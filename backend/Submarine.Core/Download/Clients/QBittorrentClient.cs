using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     <see cref="IDownloadClient" /> for the qBittorrent WebUI API v2
/// </summary>
public class QBittorrentClient : IDownloadClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly Regex MagnetHashRegex = new("xt=urn:btih:([a-zA-Z0-9]+)", RegexOptions.Compiled);

	private readonly QBittorrentSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<QBittorrentClient> _logger;
	private readonly string _baseUrl;

	private string? _sid;

	/// <summary>
	///     Creates a new <see cref="QBittorrentClient" />
	/// </summary>
	/// <param name="settings">Connection settings</param>
	/// <param name="httpClient">http client used to talk to the WebUI</param>
	/// <param name="logger">The logger of this <see cref="QBittorrentClient" /></param>
	public QBittorrentClient(QBittorrentSettings settings, HttpClient httpClient, ILogger<QBittorrentClient> logger)
	{
		_settings = settings;
		_httpClient = httpClient;
		_logger = logger;
		_baseUrl = $"{(settings.UseSsl ? "https" : "http")}://{settings.Host}:{settings.Port}/api/v2/";
	}

	/// <inheritdoc />
	public Protocol Protocol
		=> Protocol.BITTORRENT;

	/// <inheritdoc />
	public async Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
	{
		if (release.DownloadUrl == null)
			throw new DownloadClientException($"Release {release.Title} has no download url");

		var form = new Dictionary<string, string> { ["urls"] = release.DownloadUrl };
		if (_settings.Category != null) form["category"] = _settings.Category;

		using var addResponse = await SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Post, BuildUrl("torrents/add"))
		{
			Content = new FormUrlEncodedContent(form)
		}, cancellationToken);

		var hash = ParseMagnetHash(release.DownloadUrl);
		if (hash != null) return hash;

		_logger.LogWarning(
			"Could not determine the torrent hash of {Title} from a non-magnet download url, falling back to the release guid",
			release.Title);
		return release.Guid;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		var query = _settings.Category != null ? $"?category={Uri.EscapeDataString(_settings.Category)}" : string.Empty;

		using var response = await SendAuthenticatedAsync(
			() => new HttpRequestMessage(HttpMethod.Get, BuildUrl($"torrents/info{query}")), cancellationToken);

		var torrents = await response.Content.ReadFromJsonAsync<IReadOnlyList<QBittorrentTorrent>>(JsonOptions,
			cancellationToken) ?? [];

		return torrents.Select(MapItem).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default)
	{
		var form = new Dictionary<string, string>
		{
			["hashes"] = downloadId,
			["deleteFiles"] = deleteData ? "true" : "false"
		};

		using var response = await SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Post, BuildUrl("torrents/delete"))
		{
			Content = new FormUrlEncodedContent(form)
		}, cancellationToken);
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		using var response = await SendAuthenticatedAsync(
			() => new HttpRequestMessage(HttpMethod.Get, BuildUrl("app/webapiVersion")), cancellationToken);
	}

	private async Task<HttpResponseMessage> SendAuthenticatedAsync(Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		if (_sid == null) await LoginAsync(cancellationToken);

		var response = await SendWithCookieAsync(requestFactory(), cancellationToken);

		if (response.StatusCode == HttpStatusCode.Forbidden)
		{
			response.Dispose();
			await LoginAsync(cancellationToken);
			response = await SendWithCookieAsync(requestFactory(), cancellationToken);
		}

		if (!response.IsSuccessStatusCode)
		{
			var statusCode = (int)response.StatusCode;
			response.Dispose();
			throw new DownloadClientException($"qBittorrent request failed with status {statusCode}");
		}

		return response;
	}

	private async Task<HttpResponseMessage> SendWithCookieAsync(HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		if (_sid != null) request.Headers.Add("Cookie", $"SID={_sid}");
		return await _httpClient.SendAsync(request, cancellationToken);
	}

	private async Task LoginAsync(CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("auth/login"))
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["username"] = _settings.Username ?? string.Empty,
				["password"] = _settings.Password ?? string.Empty
			})
		};

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var body = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode || body.Trim() != "Ok.")
			throw new DownloadClientException("qBittorrent login failed, check the username and password");

		if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
		{
			var sidCookie = cookies.FirstOrDefault(cookie => cookie.StartsWith("SID=", StringComparison.OrdinalIgnoreCase));
			_sid = sidCookie?.Split(';')[0].Split('=')[1];
		}

		if (_sid == null) throw new DownloadClientException("qBittorrent login did not return a session id");
	}

	private string BuildUrl(string path)
		=> _baseUrl + path;

	private static string? ParseMagnetHash(string url)
	{
		if (!url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) return null;

		var match = MagnetHashRegex.Match(url);
		return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
	}

	private static DownloadClientItem MapItem(QBittorrentTorrent torrent)
		=> new()
		{
			DownloadId = torrent.Hash,
			Title = torrent.Name,
			TotalSize = torrent.Size,
			RemainingSize = torrent.AmountLeft,
			RemainingTime = torrent.Eta is > 0 and < 8640000 ? TimeSpan.FromSeconds(torrent.Eta) : null,
			Status = MapStatus(torrent.State),
			OutputPath = torrent.SavePath,
			Category = torrent.Category
		};

	private static DownloadItemStatus MapStatus(string state)
		=> state switch
		{
			"error" or "missingFiles" => DownloadItemStatus.FAILED,
			"pausedDL" => DownloadItemStatus.PAUSED,
			"pausedUP" or "uploading" or "stalledUP" or "forcedUP" or "queuedUP" or "checkingUP"
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
		[property: JsonPropertyName("category")] string? Category);
}
