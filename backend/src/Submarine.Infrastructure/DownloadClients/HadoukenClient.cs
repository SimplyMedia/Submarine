using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Hadouken JSON-RPC API
/// </summary>
public sealed class HadoukenClient(
	HadoukenSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<HadoukenSettings>(settings, clientId, clientName, httpClient)
{
	private static readonly Version MinimumSupportedVersion = new(5, 1);

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.HADOUKEN;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string RpcUrl => Settings.Build("/api");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);
		var options = new { label = Settings.Category };

		if (magnetUrl is not null)
			await RpcAsync("webui.addTorrent", ["url", magnetUrl, options], cancellationToken);
		else
		{
			var torrentData = await GetTorrentDataAsync(release, cancellationToken);
			await RpcAsync("webui.addTorrent", ["file", Convert.ToBase64String(torrentData), options],
				cancellationToken);
		}

		return ResolveTorrentId(release, magnetUrl, release.TorrentFile);
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var result = await RpcAsync("webui.list", [], cancellationToken);

		var items = new List<DownloadClientItem>();
		if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("torrents", out var torrents)
			&& torrents.ValueKind == JsonValueKind.Array)
			foreach (var row in torrents.EnumerateArray())
				if (MapItem(row) is { } item)
					items.Add(item);

		return items;
	}

	/// <inheritdoc />
	protected override Task RemoveAsyncCore(string downloadId, bool deleteData, CancellationToken cancellationToken)
		=> RpcAsync("webui.perform", [deleteData ? "removedata" : "remove", new[] { downloadId }],
			cancellationToken);

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		var result = await RpcAsync("core.getSystemInfo", [], cancellationToken);
		var versionString = result.ValueKind == JsonValueKind.Object
			&& result.TryGetProperty("versions", out var versions)
			&& versions.TryGetProperty("hadouken", out var hadoukenVersion)
			? hadoukenVersion.GetString()
			: null;

		if (versionString is null || !Version.TryParse(versionString, out var version)
			|| version < MinimumSupportedVersion)
			throw new DownloadClientException(
				$"Hadouken {ClientName} reports version {versionString ?? "unknown"}, but version {MinimumSupportedVersion} or higher is required");
	}

	private DownloadClientItem? MapItem(JsonElement row)
	{
		if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 27)
			return null;

		var infoHash = row[0].GetString()?.ToUpperInvariant();
		if (string.IsNullOrEmpty(infoHash))
			return null;

		var label = row[11].GetString();
		if (Settings.Category is { Length: > 0 } && label != Settings.Category)
			return null;

		var name = row[2].GetString() ?? string.Empty;
		var state = row[1].GetInt32();
		var progress = row[4].GetDouble();
		var totalSize = row[3].GetInt64();
		var downloaded = row[5].GetInt64();
		var uploaded = row[6].GetInt64();
		var downloadRate = row[9].GetInt64();
		var error = row[21].GetString();
		var savePath = row[26].GetString();
		var isFinished = progress >= 1000;

		return new DownloadClientItem
		{
			DownloadId = infoHash,
			Title = name,
			Category = label,
			TotalSize = totalSize,
			RemainingSize = totalSize - downloaded,
			RemainingTime = downloadRate > 0 && totalSize > downloaded
				? TimeSpan.FromSeconds((totalSize - downloaded) / (double)downloadRate)
				: null,
			OutputPath = string.IsNullOrEmpty(savePath) ? null : $"{savePath.TrimEnd('/', '\\')}/{name}",
			Status = MapStatus(state, isFinished, error),
			Message = string.IsNullOrEmpty(error) ? null : error,
			SeedRatio = downloaded > 0 ? (double)uploaded / downloaded : 0
		};
	}

	private static DownloadItemStatus MapStatus(int state, bool isFinished, string? error)
	{
		if (!string.IsNullOrEmpty(error))
			return DownloadItemStatus.WARNING;

		if (isFinished && (state & 2) == 0)
			return DownloadItemStatus.COMPLETED;

		if ((state & 64) == 64)
			return DownloadItemStatus.QUEUED;

		if ((state & 32) == 32)
			return DownloadItemStatus.PAUSED;

		return DownloadItemStatus.DOWNLOADING;
	}

	private async Task<JsonElement> RpcAsync(string method, IReadOnlyList<object?> parameters,
		CancellationToken cancellationToken)
	{
		using var response = await SendAsync(
			() =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, RpcUrl)
				{
					Content = new StringContent(JsonSerializer.Serialize(new { method, @params = parameters }),
						Encoding.UTF8, "application/json")
				};

				if (BasicAuth(Settings.Username, Settings.Password) is { } auth)
					request.Headers.Authorization = auth;

				return request;
			}, cancellationToken);

		EnsureSuccess(response, $"Hadouken {ClientName} request");

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
			throw new DownloadClientException($"Hadouken {ClientName} request failed: {errorElement}");

		return root.TryGetProperty("result", out var result) ? result.Clone() : default;
	}
}
