using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the RQBit REST api
/// </summary>
public sealed class RQbitClient(
	RQbitSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<RQbitSettings>(settings, clientId, clientName, httpClient)
{
	private static readonly Version MinimumSupportedVersion = new(8, 0, 0);

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.RQBIT;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);
		HttpContent content = magnetUrl is not null
			? new StringContent(magnetUrl)
			: new ByteArrayContent(await GetTorrentDataAsync(release, cancellationToken));

		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Settings.Build("/torrents?overwrite=true"))
			{
				Content = content
			}, cancellationToken);

		EnsureSuccess(response, $"RQBit {ClientName} add torrent");
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);

		var hash = body.TryGetProperty("details", out var details)
			&& details.TryGetProperty("info_hash", out var infoHash)
			? infoHash.GetString()
			: null;

		return (hash ?? ResolveTorrentId(release, magnetUrl, release.TorrentFile)).ToUpperInvariant();
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Get, Settings.Build("/torrents?with_stats=true")),
			cancellationToken);

		if (!response.IsSuccessStatusCode)
			return [];

		var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);

		var items = new List<DownloadClientItem>();
		if (body.TryGetProperty("torrents", out var torrents) && torrents.ValueKind == JsonValueKind.Array)
			foreach (var torrent in torrents.EnumerateArray())
				if (MapItem(torrent) is { } item)
					items.Add(item);

		return items;
	}

	private DownloadClientItem? MapItem(JsonElement torrent)
	{
		var infoHash = torrent.TryGetProperty("info_hash", out var hashEl) ? hashEl.GetString() : null;
		if (string.IsNullOrEmpty(infoHash))
			return null;

		var outputFolder = torrent.TryGetProperty("output_folder", out var folderEl) ? folderEl.GetString() : null;
		var name = torrent.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;

		var path = outputFolder is null
			? null
			: outputFolder.EndsWith(name, StringComparison.Ordinal) ? outputFolder : outputFolder + name;

		if (string.IsNullOrEmpty(path) || path.StartsWith('.'))
			return null;

		if (!torrent.TryGetProperty("stats", out var stats))
			return null;

		var totalBytes = stats.TryGetProperty("total_bytes", out var totalEl) ? totalEl.GetInt64() : 0;
		var progressBytes = stats.TryGetProperty("progress_bytes", out var progressEl) ? progressEl.GetInt64() : 0;
		var uploadedBytes = stats.TryGetProperty("uploaded_bytes", out var uploadedEl) ? uploadedEl.GetInt64() : 0;
		var finished = stats.TryGetProperty("finished", out var finishedEl) && finishedEl.GetBoolean();
		var state = stats.TryGetProperty("state", out var stateEl) ? stateEl.GetString() : null;
		var error = stats.TryGetProperty("error", out var errorEl) ? errorEl.GetString() : null;

		var downRate = 0.0;
		if (stats.TryGetProperty("live", out var live) && live.ValueKind == JsonValueKind.Object
			&& live.TryGetProperty("download_speed", out var speed) && speed.TryGetProperty("mbps", out var mbps))
			downRate = mbps.GetDouble() * 1_048_576;

		var remaining = totalBytes - progressBytes;
		var ratio = progressBytes > 0 ? (double)uploadedBytes / progressBytes : 0;
		var isActive = state is "Live" or "Initializing";

		return new DownloadClientItem
		{
			DownloadId = infoHash.ToUpperInvariant(),
			Title = name,
			TotalSize = totalBytes,
			RemainingSize = remaining,
			RemainingTime = downRate > 0 ? TimeSpan.FromSeconds(remaining / downRate) : TimeSpan.Zero,
			SeedRatio = ratio,
			OutputPath = path,
			Status = MapStatus(finished, isActive, error),
			Message = string.IsNullOrEmpty(error) ? null : error
		};
	}

	private static DownloadItemStatus MapStatus(bool finished, bool isActive, string? error)
	{
		if (!string.IsNullOrEmpty(error))
			return DownloadItemStatus.FAILED;

		if (finished)
			return DownloadItemStatus.COMPLETED;

		return isActive ? DownloadItemStatus.DOWNLOADING : DownloadItemStatus.PAUSED;
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		var resource = deleteData ? "/delete" : "/forget";
		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Settings.Build($"/torrents/{downloadId}{resource}")),
			cancellationToken);

		EnsureSuccess(response, $"RQBit {ClientName} remove torrent");
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Settings.Build("/")),
			cancellationToken);
		EnsureSuccess(response, $"RQBit {ClientName} version check");

		var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
		var versionString = body.TryGetProperty("version", out var versionEl) ? versionEl.GetString() : null;

		if (string.IsNullOrWhiteSpace(versionString))
			throw new DownloadClientException(
				$"RQBit {ClientName} did not report a version; check that it is running and accessible");

		var match = System.Text.RegularExpressions.Regex.Match(versionString, @"(\d+)\.(\d+)\.(\d+)");
		if (!match.Success)
			throw new DownloadClientException($"RQBit {ClientName} reports an unparseable version '{versionString}'");

		var version = new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
			int.Parse(match.Groups[3].Value));

		if (version < MinimumSupportedVersion)
			throw new DownloadClientException(
				$"RQBit {ClientName} reports version {version}, but version {MinimumSupportedVersion} or higher is required");
	}
}
