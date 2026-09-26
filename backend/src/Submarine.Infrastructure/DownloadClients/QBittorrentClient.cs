using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the qBittorrent WebUI API v2
/// </summary>
public sealed class QBittorrentClient(
	QBittorrentSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<QBittorrentSettings>(settings, clientId, clientName, httpClient)
{
	private string? _sid;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.QBITTORRENT;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string Api(string path)
		=> Settings.Build($"/api/v2{path}");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);

		if (magnetUrl is not null)
		{
			var fields = AddFields(seedCriteria);
			fields.Add(new KeyValuePair<string, string>("urls", magnetUrl));

			await AuthenticatedAsync(
				() => new HttpRequestMessage(HttpMethod.Post, Api("/torrents/add"))
				{
					Content = new FormUrlEncodedContent(fields)
				}, cancellationToken);
		}
		else
		{
			var torrentData = await GetTorrentDataAsync(release, cancellationToken);

			await AuthenticatedAsync(() =>
			{
				var content = new MultipartFormDataContent();
				var file = new ByteArrayContent(torrentData);
				file.Headers.ContentType = new MediaTypeHeaderValue("application/x-bittorrent");
				content.Add(file, "torrents", $"{SanitizeFileName(release.Title)}.torrent");

				foreach (var (key, value) in AddFields(seedCriteria))
					content.Add(new StringContent(value), key);

				return new HttpRequestMessage(HttpMethod.Post, Api("/torrents/add")) { Content = content };
			}, cancellationToken);
		}

		var downloadId = ResolveTorrentId(release, magnetUrl, release.TorrentFile);

		if (Settings.InitialState == QBittorrentInitialState.FORCE_START)
			await AuthenticatedAsync(
				() => new HttpRequestMessage(HttpMethod.Post, Api("/torrents/setForceStart"))
				{
					Content = new FormUrlEncodedContent(
					[
						new KeyValuePair<string, string>("hashes", downloadId),
						new KeyValuePair<string, string>("value", "true")
					])
				}, cancellationToken);

		return downloadId;
	}

	private List<KeyValuePair<string, string>> AddFields(SeedCriteria? seedCriteria)
	{
		var fields = new List<KeyValuePair<string, string>>();

		if (Settings.Category is { Length: > 0 })
			fields.Add(new KeyValuePair<string, string>("category", Settings.Category));

		if (Settings.InitialState == QBittorrentInitialState.PAUSE)
			fields.Add(new KeyValuePair<string, string>("stopped", "true"));

		if (Settings.SequentialOrder)
			fields.Add(new KeyValuePair<string, string>("sequentialDownload", "true"));

		if (Settings.FirstAndLast)
			fields.Add(new KeyValuePair<string, string>("firstLastPiecePrio", "true"));

		// season pack seed time is not distinguishable at add, so the regular seed time wins when both are set
		if (seedCriteria?.Ratio is { } ratio)
			fields.Add(new KeyValuePair<string, string>("ratioLimit", ratio.ToString(CultureInfo.InvariantCulture)));

		if ((seedCriteria?.SeedTimeMinutes ?? seedCriteria?.SeasonPackSeedTimeMinutes) is { } minutes)
			fields.Add(new KeyValuePair<string, string>("seedingTimeLimit",
				minutes.ToString(CultureInfo.InvariantCulture)));

		return fields;
	}

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		var query = Settings.Category is { Length: > 0 }
			? $"?category={Uri.EscapeDataString(Settings.Category)}"
			: string.Empty;

		var response = await AuthenticatedAsync(
			() => new HttpRequestMessage(HttpMethod.Get, Api($"/torrents/info{query}")), cancellationToken);

		var torrents = await response.Content.ReadFromJsonAsync<IReadOnlyList<QBittorrentTorrent>>(JsonOptions,
			cancellationToken) ?? [];

		return [.. torrents.Select(MapItem)];
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
		=> await AuthenticatedAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Api("/torrents/delete"))
			{
				Content = new FormUrlEncodedContent(
				[
					new KeyValuePair<string, string>("hashes", downloadId),
					new KeyValuePair<string, string>("deleteFiles", deleteData ? "true" : "false")
				])
			}, cancellationToken);

	/// <inheritdoc />
	public override async Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(Settings.PostImportCategory))
			return;

		await AuthenticatedAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Api("/torrents/setCategory"))
			{
				Content = new FormUrlEncodedContent(
				[
					new KeyValuePair<string, string>("hashes", downloadId),
					new KeyValuePair<string, string>("category", Settings.PostImportCategory)
				])
			}, cancellationToken);
	}

	/// <inheritdoc />
	protected override Task TestAsyncCore(CancellationToken cancellationToken)
		=> AuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, Api("/app/webapiVersion")),
			cancellationToken);

	private async Task<HttpResponseMessage> AuthenticatedAsync(Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		if (_sid is null)
			await LoginAsync(cancellationToken);

		var response = await SendWithCookieAsync(requestFactory(), cancellationToken);

		if (response.StatusCode == HttpStatusCode.Forbidden)
		{
			response.Dispose();
			await LoginAsync(cancellationToken);
			response = await SendWithCookieAsync(requestFactory(), cancellationToken);
		}

		EnsureSuccess(response, $"qBittorrent {ClientName} request");

		return response;
	}

	private async Task<HttpResponseMessage> SendWithCookieAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		if (_sid is not null)
			request.Headers.Add("Cookie", $"SID={_sid}");

		return await SendAsync(() => request, cancellationToken);
	}

	private async Task LoginAsync(CancellationToken cancellationToken)
	{
		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Api("/auth/login"))
			{
				Content = new FormUrlEncodedContent(
				[
					new KeyValuePair<string, string>("username", Settings.Username ?? string.Empty),
					new KeyValuePair<string, string>("password", Settings.Password ?? string.Empty)
				])
			}, cancellationToken);

		var body = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode || body.Trim() != "Ok.")
			throw new DownloadClientException("qBittorrent login failed, check the username and password");

		if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
			_sid = cookies.FirstOrDefault(cookie => cookie.StartsWith("SID=", StringComparison.OrdinalIgnoreCase))
				?.Split(';')[0].Split('=')[1];

		if (_sid is null)
			throw new DownloadClientException("qBittorrent login did not return a session id");
	}

	private DownloadClientItem MapItem(QBittorrentTorrent torrent)
		=> new()
		{
			DownloadId = torrent.Hash,
			Title = torrent.Name,
			TotalSize = torrent.Size,
			RemainingSize = torrent.AmountLeft,
			RemainingTime = torrent.Eta is > 0 and < 8640000 ? TimeSpan.FromSeconds(torrent.Eta) : null,
			Status = MapStatus(torrent.State),
			OutputPath = torrent.SavePath,
			Category = string.IsNullOrEmpty(torrent.Category) ? null : torrent.Category,
			IsReadOnly = Settings.Category is { Length: > 0 } && torrent.Category != Settings.Category,
			SeedRatio = torrent.MaxRatio >= 0 ? torrent.MaxRatio : null,
			SeedTime = torrent.MaxSeedingTime > 0 ? TimeSpan.FromSeconds(torrent.MaxSeedingTime) : null
		};

	private static DownloadItemStatus MapStatus(string state)
		=> state switch
		{
			"error" or "missingFiles" => DownloadItemStatus.FAILED,
			"pausedDL" or "stoppedDL" => DownloadItemStatus.PAUSED,
			"pausedUP" or "stoppedUP" or "uploading" or "stalledUP" or "forcedUP" or "queuedUP" or "checkingUP"
				=> DownloadItemStatus.COMPLETED,
			"downloading" or "metaDL" or "forcedDL" or "stalledDL" or "checkingDL" or "allocating" or "moving"
				=> DownloadItemStatus.DOWNLOADING,
			"queuedDL" or "checkingResumeData" => DownloadItemStatus.QUEUED,
			_ => DownloadItemStatus.WARNING
		};

	private record QBittorrentTorrent(
		[property: JsonPropertyName("hash")] string Hash,
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("size")] long Size,
		[property: JsonPropertyName("amount_left")] long AmountLeft,
		[property: JsonPropertyName("eta")] long Eta,
		[property: JsonPropertyName("state")] string State,
		[property: JsonPropertyName("save_path")] string? SavePath,
		[property: JsonPropertyName("category")] string? Category,
		[property: JsonPropertyName("max_ratio")] double MaxRatio,
		[property: JsonPropertyName("max_seeding_time")] long MaxSeedingTime);
}
