using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to the Deluge web ui over JSON-RPC
/// </summary>
public class DelugeClient : IDownloadClient
{
	private static readonly IReadOnlyList<object?> ItemFields = new object?[]
	{
		"name", "hash", "total_size", "total_done", "eta", "state", "progress", "save_path", "label", "message"
	};

	private readonly DelugeSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<DelugeClient> _logger;

	private int _requestId;
	private bool _authenticated;

	/// <summary>
	///     Creates a new instance of <see cref="DelugeClient" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for the JSON-RPC calls</param>
	/// <param name="logger">logger</param>
	public DelugeClient(DelugeSettings settings, HttpClient httpClient, ILogger<DelugeClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
	}

	/// <inheritdoc />
	public Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, SeedCriteria? seedCriteria = default,
		CancellationToken cancellationToken = default)
	{
		var url = release.DownloadUrl
			?? throw new DownloadClientException("Release has no download url");

		var method = url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
			? "core.add_torrent_magnet"
			: "core.add_torrent_url";

		var options = new Dictionary<string, object?>();

		if (seedCriteria?.Ratio is { } ratio)
		{
			options["stop_at_ratio"] = true;
			options["stop_ratio"] = ratio;
			options["remove_at_ratio"] = false;
		}

		var result = await RpcAsync(method, new object?[] { url, options },
			cancellationToken);

		var hash = result.GetString()
			?? throw new DownloadClientException("Deluge did not return a torrent hash");

		if (_settings.Category is { Length: > 0 } category)
			try
			{
				await RpcAsync("label.set_torrent", new object?[] { hash, category }, cancellationToken);
			}
			catch (DownloadClientException e)
			{
				_logger.LogDebug(e, "Failed to set Deluge label {Label} on {Hash}", category, hash);
			}

		return hash;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var result = await RpcAsync("web.update_ui", new object?[] { ItemFields, new Dictionary<string, object?>() },
			cancellationToken);

		return result.GetProperty("torrents").EnumerateObject().Select(t => MapItem(t.Value)).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
		=> await RpcAsync("core.remove_torrent", new object?[] { downloadId, deleteData }, cancellationToken);

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await RpcAsync("daemon.info", [], cancellationToken);
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach Deluge", e);
		}
	}

	private static DownloadClientItem MapItem(JsonElement torrent)
	{
		var totalSize = torrent.GetProperty("total_size").GetInt64();
		var totalDone = torrent.GetProperty("total_done").GetInt64();
		var eta = torrent.GetProperty("eta").GetDouble();
		var label = torrent.TryGetProperty("label", out var l) ? l.GetString() : null;

		return new DownloadClientItem
		{
			DownloadId = torrent.GetProperty("hash").GetString()!,
			Title = torrent.GetProperty("name").GetString()!,
			TotalSize = totalSize,
			RemainingSize = totalSize - totalDone,
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = MapState(torrent.GetProperty("state").GetString()),
			OutputPath = torrent.TryGetProperty("save_path", out var sp) ? sp.GetString() : null,
			Category = string.IsNullOrEmpty(label) ? null : label,
			Message = torrent.TryGetProperty("message", out var m) ? m.GetString() : null
		};
	}

	private static DownloadItemStatus MapState(string? state)
		=> state switch
		{
			"Downloading" => DownloadItemStatus.DOWNLOADING,
			"Paused" => DownloadItemStatus.PAUSED,
			"Seeding" => DownloadItemStatus.COMPLETED,
			"Error" => DownloadItemStatus.FAILED,
			_ => DownloadItemStatus.QUEUED
		};

	private async Task<JsonElement> RpcAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		await LoginIfNeededAsync(cancellationToken);

		var (result, error) = await SendAsync(method, parameters, cancellationToken);

		if (error is not null && error.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase))
		{
			_authenticated = false;
			await LoginIfNeededAsync(cancellationToken);
			(result, error) = await SendAsync(method, parameters, cancellationToken);
		}

		if (error is not null)
			throw new DownloadClientException($"Deluge error: {error}");

		return result;
	}

	private async Task LoginIfNeededAsync(CancellationToken cancellationToken)
	{
		if (_authenticated)
			return;

		var (result, error) = await SendAsync("auth.login", new object?[] { _settings.Password }, cancellationToken);

		if (error is not null || result.ValueKind != JsonValueKind.True)
			throw new DownloadClientException("Deluge authentication failed");

		_authenticated = true;
	}

	private async Task<(JsonElement Result, string? Error)> SendAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		var body = JsonSerializer.Serialize(new { method, @params = parameters, id = ++_requestId });

		using var response = await _httpClient.PostAsync(_settings.Endpoint,
			new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
		response.EnsureSuccessStatusCode();

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		string? error = null;
		if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
			error = err.TryGetProperty("message", out var message) ? message.GetString() : "unknown";

		var result = root.TryGetProperty("result", out var value) ? value.Clone() : default;

		return (result, error);
	}
}
