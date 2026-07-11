using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to aria2 over JSON-RPC
/// </summary>
public class Aria2Client : IDownloadClient
{
	private readonly Aria2Settings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<Aria2Client> _logger;

	/// <summary>
	///     Creates a new instance of <see cref="Aria2Client" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for the JSON-RPC calls</param>
	/// <param name="logger">logger</param>
	public Aria2Client(Aria2Settings settings, HttpClient httpClient, ILogger<Aria2Client> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
	}

	/// <inheritdoc />
	public Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		var url = release.DownloadUrl
			?? throw new DownloadClientException("Release has no download url");

		var result = await RpcAsync("aria2.addUri", new object?[] { new object?[] { url } }, cancellationToken);

		return result.GetString()
			?? throw new DownloadClientException("aria2 did not return a gid");
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var active = await RpcAsync("aria2.tellActive", [], cancellationToken);
		var waiting = await RpcAsync("aria2.tellWaiting", new object?[] { 0, 1000 }, cancellationToken);
		var stopped = await RpcAsync("aria2.tellStopped", new object?[] { 0, 1000 }, cancellationToken);

		return active.EnumerateArray()
			.Concat(waiting.EnumerateArray())
			.Concat(stopped.EnumerateArray())
			.Select(MapItem)
			.ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await RpcAsync("aria2.remove", new object?[] { downloadId }, cancellationToken);
		}
		catch (DownloadClientException e)
		{
			_logger.LogDebug(e, "aria2 could not remove active download {Gid}, clearing its result instead",
				downloadId);
		}

		await RpcAsync("aria2.removeDownloadResult", new object?[] { downloadId }, cancellationToken);
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await RpcAsync("aria2.getVersion", [], cancellationToken);
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach aria2", e);
		}
	}

	private static DownloadClientItem MapItem(JsonElement download)
	{
		var total = long.Parse(download.GetProperty("totalLength").GetString()!, CultureInfo.InvariantCulture);
		var completed = long.Parse(download.GetProperty("completedLength").GetString()!, CultureInfo.InvariantCulture);
		var gid = download.GetProperty("gid").GetString()!;

		string? path = null;
		if (download.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
			path = files[0].GetProperty("path").GetString();

		return new DownloadClientItem
		{
			DownloadId = gid,
			Title = string.IsNullOrEmpty(path) ? gid : Path.GetFileName(path),
			TotalSize = total,
			RemainingSize = total - completed,
			Status = MapStatus(download.GetProperty("status").GetString()),
			OutputPath = download.TryGetProperty("dir", out var dir) ? dir.GetString() : null
		};
	}

	private static DownloadItemStatus MapStatus(string? status)
		=> status switch
		{
			"active" => DownloadItemStatus.DOWNLOADING,
			"waiting" => DownloadItemStatus.QUEUED,
			"paused" => DownloadItemStatus.PAUSED,
			"complete" => DownloadItemStatus.COMPLETED,
			"error" => DownloadItemStatus.FAILED,
			_ => DownloadItemStatus.QUEUED
		};

	private async Task<JsonElement> RpcAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		var fullParameters = new List<object?> { $"token:{_settings.Secret}" };
		fullParameters.AddRange(parameters);

		var body = JsonSerializer.Serialize(new
		{
			jsonrpc = "2.0", id = Guid.NewGuid().ToString("N"), method, @params = fullParameters
		});

		using var response = await _httpClient.PostAsync(_settings.Endpoint,
			new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
		response.EnsureSuccessStatusCode();

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
			throw new DownloadClientException(
				$"aria2 error: {(error.TryGetProperty("message", out var m) ? m.GetString() : "unknown")}");

		return root.GetProperty("result").Clone();
	}
}
