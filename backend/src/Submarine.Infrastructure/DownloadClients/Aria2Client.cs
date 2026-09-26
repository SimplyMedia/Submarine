using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for aria2 over JSON-RPC
/// </summary>
public sealed class Aria2Client(
	Aria2Settings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<Aria2Settings>(settings, clientId, clientName, httpClient)
{
	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.ARIA2;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string RpcUrl => Settings.RpcUrl;

	private string TokenPrefix => $"token:{Settings.SecretToken ?? string.Empty}";

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);

		if (magnetUrl is not null)
		{
			var result = await RpcAsync("aria2.addUri", new object?[] { new object?[] { magnetUrl }, DirectoryOptions(seedCriteria) },
				cancellationToken);

			return result.GetString() ?? ResolveTorrentId(release, magnetUrl, null);
		}

		var torrentData = await GetTorrentDataAsync(release, cancellationToken);
		var result2 = await RpcAsync("aria2.addTorrent",
			new object?[] { Convert.ToBase64String(torrentData), Array.Empty<object?>(), DirectoryOptions(seedCriteria) },
			cancellationToken);

		return result2.GetString() ?? ResolveTorrentId(release, magnetUrl, torrentData);
	}

	private Dictionary<string, object?> DirectoryOptions(SeedCriteria? seedCriteria)
	{
		var options = new Dictionary<string, object?>();
		if (Settings.Directory is { Length: > 0 })
			options["dir"] = Settings.Directory;

		if (seedCriteria?.Ratio is { } ratio)
			options["seed-ratio"] = ratio.ToString(CultureInfo.InvariantCulture);

		if ((seedCriteria?.SeedTimeMinutes ?? seedCriteria?.SeasonPackSeedTimeMinutes) is { } minutes)
			options["seed-time"] = minutes.ToString(CultureInfo.InvariantCulture);

		return options;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var active = await RpcAsync("aria2.tellActive", [], cancellationToken);
		var waiting = await RpcAsync("aria2.tellWaiting", [0, 1000], cancellationToken);
		var stopped = await RpcAsync("aria2.tellStopped", [0, 1000], cancellationToken);

		return [.. active.EnumerateArray()
			.Concat(waiting.EnumerateArray())
			.Concat(stopped.EnumerateArray())
			.Select(MapItem)];
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
	{
		try
		{
			await RpcAsync("aria2.remove", [downloadId], cancellationToken);
		}
		catch (DownloadClientException)
		{
			// already finished downloads cannot be removed, only their result can
		}

		await RpcAsync("aria2.removeDownloadResult", [downloadId], cancellationToken);
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> RpcAsync("aria2.getVersion", [], cancellationToken);

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
		var fullParameters = new List<object?> { TokenPrefix };
		fullParameters.AddRange(parameters);

		var body = JsonSerializer.Serialize(new
		{
			jsonrpc = "2.0",
			id = Guid.NewGuid().ToString("N"),
			method,
			@params = fullParameters
		});

		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Post, RpcUrl)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			}, cancellationToken);
		EnsureSuccess(response, $"aria2 {ClientName} request");

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
			throw new DownloadClientException(
				$"aria2 {ClientName} error: {(error.TryGetProperty("message", out var message) ? message.GetString() : "unknown")}");

		return root.GetProperty("result").Clone();
	}
}
