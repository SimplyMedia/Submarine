using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Deluge web ui over JSON-RPC
/// </summary>
public sealed class DelugeClient(
	DelugeSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<DelugeSettings>(settings, clientId, clientName, httpClient)
{
	private static readonly string[] ItemFields =
		["name", "hash", "total_size", "total_done", "eta", "state", "save_path", "label", "message"];

	private int _requestId;
	private bool _authenticated;
	private string? _sessionCookie;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.DELUGE;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string RpcUrl => Settings.Build("/json");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);

		var options = new Dictionary<string, object?>();
		if (Settings.AddPaused)
			options["add_paused"] = true;

		if (seedCriteria?.Ratio is { } ratio)
		{
			options["stop_at_ratio"] = true;
			options["stop_ratio"] = ratio;
			options["remove_at_ratio"] = false;
		}

		var result = magnetUrl is not null
			? await RpcAsync("core.add_torrent_magnet", [magnetUrl, options], cancellationToken)
			: await AddTorrentFileAsync(release, options, cancellationToken);

		var hash = result.ValueKind == JsonValueKind.String
			? result.GetString() ?? string.Empty
			: result.GetRawText();

		if (Settings.Category is { Length: > 0 })
		{
			try
			{
				await RpcAsync("label.set_torrent", [hash, Settings.Category], cancellationToken);
			}
			catch (DownloadClientException)
			{
				// the label plugin is optional; adding the torrent already succeeded
			}
		}

		return hash;
	}

	private async Task<JsonElement> AddTorrentFileAsync(RemoteRelease release, Dictionary<string, object?> options,
		CancellationToken cancellationToken)
	{
		var torrentData = await GetTorrentDataAsync(release, cancellationToken);

		return await RpcAsync("core.add_torrent_file",
			[$"{SanitizeFileName(release.Title)}.torrent", Convert.ToBase64String(torrentData), options],
			cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var result = await RpcAsync("core.get_torrents_status", [new Dictionary<string, object?>(), ItemFields],
			cancellationToken);

		return [.. result.EnumerateObject().Select(property => MapItem(property.Name, property.Value))];
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> RpcAsync("core.remove_torrent", [downloadId, deleteData], cancellationToken);

	/// <inheritdoc />
	public override async Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(Settings.PostImportCategory) || Settings.PostImportCategory == Settings.Category)
			return;

		try
		{
			await RpcAsync("label.set_torrent", [downloadId.ToLowerInvariant(), Settings.PostImportCategory],
				cancellationToken);
		}
		catch (DownloadClientException)
		{
			// the label plugin is optional; the download was already added successfully
		}
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> RpcAsync("daemon.info", [], cancellationToken);

	private DownloadClientItem MapItem(string hash, JsonElement torrent)
	{
		var totalSize = torrent.GetProperty("total_size").GetInt64();
		var totalDone = torrent.GetProperty("total_done").GetInt64();
		var eta = torrent.GetProperty("eta").GetDouble();
		var label = torrent.TryGetProperty("label", out var labelElement) ? labelElement.GetString() : null;

		return new DownloadClientItem
		{
			DownloadId = hash,
			Title = torrent.GetProperty("name").GetString() ?? string.Empty,
			TotalSize = totalSize,
			RemainingSize = totalSize - totalDone,
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = MapState(torrent.GetProperty("state").GetString()),
			OutputPath = torrent.TryGetProperty("save_path", out var savePath) ? savePath.GetString() : null,
			Category = string.IsNullOrEmpty(label) ? null : label,
			Message = torrent.TryGetProperty("message", out var message) ? message.GetString() : null,
			IsReadOnly = Settings.Category is { Length: > 0 } && label != Settings.Category
		};
	}

	private static DownloadItemStatus MapState(string? state)
		=> state switch
		{
			"Downloading" => DownloadItemStatus.DOWNLOADING,
			"Checking" or "Moving" or "Queued" => DownloadItemStatus.QUEUED,
			"Paused" => DownloadItemStatus.PAUSED,
			"Seeding" => DownloadItemStatus.COMPLETED,
			"Error" => DownloadItemStatus.FAILED,
			_ => DownloadItemStatus.QUEUED
		};

	private async Task<JsonElement> RpcAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		await LoginIfNeededAsync(cancellationToken);

		return await SendAsync(method, parameters, cancellationToken);
	}

	private async Task LoginIfNeededAsync(CancellationToken cancellationToken)
	{
		if (_authenticated)
			return;

		var result = await SendAsync("auth.login", [Settings.Password], cancellationToken);

		if (result.ValueKind != JsonValueKind.True)
			throw new DownloadClientException($"Deluge {ClientName} authentication failed, check the password");

		_authenticated = true;
	}

	private async Task<JsonElement> SendAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		var (root, error) = await PostAsync(
			JsonSerializer.Serialize(new { method, @params = parameters, id = ++_requestId }), cancellationToken);

		if (error is not null && error.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase))
		{
			_authenticated = false;
			await LoginIfNeededAsync(cancellationToken);

			(root, error) = await PostAsync(
				JsonSerializer.Serialize(new { method, @params = parameters, id = ++_requestId }),
				cancellationToken);
		}

		if (error is not null)
			throw new DownloadClientException($"Deluge {ClientName} error: {error}");

		return root.TryGetProperty("result", out var result) ? result.Clone() : default;
	}

	private async Task<(JsonElement Root, string? Error)> PostAsync(string body, CancellationToken cancellationToken)
	{
		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, RpcUrl)
				{
					Content = new StringContent(body, Encoding.UTF8, "application/json")
				};

				if (_sessionCookie is not null)
					request.Headers.Add("Cookie", _sessionCookie);

				return request;
			}, cancellationToken);

		EnsureSuccess(response, $"Deluge {ClientName} request");

		if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
			_sessionCookie = cookies.FirstOrDefault()?.Split(';')[0];

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		string? error = null;
		if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.Object)
			error = errorElement.TryGetProperty("message", out var message) ? message.GetString() : "unknown error";

		return (root.Clone(), error);
	}
}
