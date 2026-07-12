using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download.Clients;

/// <summary>
///     Download client talking to the Flood rest api
/// </summary>
public class FloodClient : IDownloadClient
{
	private readonly FloodSettings _settings;
	private readonly HttpClient _httpClient;
	private readonly ILogger<FloodClient> _logger;

	private bool _authenticated;

	/// <summary>
	///     Creates a new instance of <see cref="FloodClient" />
	/// </summary>
	/// <param name="settings">connection settings</param>
	/// <param name="httpClient">http client used for the rest calls</param>
	/// <param name="logger">logger</param>
	public FloodClient(FloodSettings settings, HttpClient httpClient, ILogger<FloodClient> logger)
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

		if (seedCriteria != null)
			_logger.LogDebug("seed criteria not supported by Flood");

		var hash = MagnetHash(url)
			?? throw new DownloadClientException("Flood add requires a magnet link to resolve the download id");

		var payload = new Dictionary<string, object?> { ["urls"] = new[] { url } };
		if (_settings.Destination is { Length: > 0 } destination)
			payload["destination"] = destination;
		if (_settings.Category is { Length: > 0 } category)
			payload["tags"] = new[] { category };

		using var response = await SendAsync(
			() => JsonRequest(HttpMethod.Post, "/api/torrents/add-urls", payload), cancellationToken);
		response.EnsureSuccessStatusCode();

		return hash;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default)
	{
		using var response = await SendAsync(
			() => new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}/api/torrents"), cancellationToken);
		response.EnsureSuccessStatusCode();

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		return document.RootElement.GetProperty("torrents").EnumerateObject()
			.Select(t => MapItem(t.Name, t.Value)).ToList();
	}

	/// <inheritdoc />
	public async Task RemoveItemAsync(string downloadId, bool deleteData,
		CancellationToken cancellationToken = default)
	{
		var payload = new { hashes = new[] { downloadId }, deleteData };

		using var response = await SendAsync(
			() => JsonRequest(HttpMethod.Post, "/api/torrents/delete", payload), cancellationToken);
		response.EnsureSuccessStatusCode();
	}

	/// <inheritdoc />
	public async Task TestAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await SendAsync(
				() => new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}/api/client/connection-test"),
				cancellationToken);
			response.EnsureSuccessStatusCode();
		}
		catch (DownloadClientException)
		{
			throw;
		}
		catch (Exception e)
		{
			throw new DownloadClientException("Failed to reach Flood", e);
		}
	}

	private static DownloadClientItem MapItem(string hash, JsonElement torrent)
	{
		var size = torrent.GetProperty("sizeBytes").GetInt64();
		var done = torrent.GetProperty("bytesDone").GetInt64();
		var eta = torrent.TryGetProperty("eta", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0;
		string? tag = torrent.TryGetProperty("tags", out var tags) && tags.GetArrayLength() > 0
			? tags[0].GetString()
			: null;

		return new DownloadClientItem
		{
			DownloadId = hash,
			Title = torrent.GetProperty("name").GetString()!,
			TotalSize = size,
			RemainingSize = size - done,
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = MapStatus(torrent.GetProperty("status")),
			OutputPath = torrent.TryGetProperty("directory", out var d) ? d.GetString() : null,
			Category = tag
		};
	}

	private static DownloadItemStatus MapStatus(JsonElement status)
	{
		var states = status.EnumerateArray().Select(s => s.GetString()).ToHashSet();

		if (states.Contains("error"))
			return DownloadItemStatus.FAILED;
		if (states.Contains("complete"))
			return DownloadItemStatus.COMPLETED;
		if (states.Contains("downloading"))
			return DownloadItemStatus.DOWNLOADING;
		if (states.Contains("stopped"))
			return DownloadItemStatus.PAUSED;

		return DownloadItemStatus.QUEUED;
	}

	private static string? MagnetHash(string url)
	{
		var match = Regex.Match(url, "xt=urn:btih:([^&]+)", RegexOptions.IgnoreCase);

		return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
	}

	private HttpRequestMessage JsonRequest(HttpMethod method, string path, object body)
		=> new(method, $"{_settings.BaseUrl}{path}") { Content = JsonContent.Create(body) };

	private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await _httpClient.SendAsync(requestFactory(), cancellationToken);

		if (response.StatusCode == HttpStatusCode.Unauthorized)
		{
			response.Dispose();
			_authenticated = false;
			await EnsureAuthenticatedAsync(cancellationToken);
			response = await _httpClient.SendAsync(requestFactory(), cancellationToken);
		}

		return response;
	}

	private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
	{
		if (_authenticated)
			return;

		using var response = await _httpClient.PostAsync($"{_settings.BaseUrl}/api/auth/authenticate",
			JsonContent.Create(new { username = _settings.Username, password = _settings.Password }),
			cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new DownloadClientException("Flood authentication failed");

		_authenticated = true;
	}
}
