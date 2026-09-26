using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     <see cref="IDownloadClient" /> for the Flood rest api
/// </summary>
public sealed class FloodClient(
	FloodSettings settings,
	int clientId,
	string clientName,
	HttpClient httpClient) : DownloadClientBase<FloodSettings>(settings, clientId, clientName, httpClient)
{
	private bool _authenticated;
	private string? _authCookie;

	/// <inheritdoc />
	public override DownloadClientType Type => DownloadClientType.FLOOD;

	/// <inheritdoc />
	public override Protocol Protocol => Protocol.BITTORRENT;

	private string Api(string path) => Settings.Build($"/api{path}");

	/// <inheritdoc />
	protected override async Task<string> AddAsyncCore(RemoteRelease release, SeedCriteria? seedCriteria,
		CancellationToken cancellationToken)
	{
		var magnetUrl = MagnetUrl(release);
		var fileName = $"{SanitizeFileName(release.Title)}.torrent";
		var tags = BuildTags(release);

		if (magnetUrl is not null)
		{
			var payload = new Dictionary<string, object?>
			{
				["urls"] = new[] { magnetUrl },
				["stopped"] = Settings.AddPaused,
				["tags"] = tags
			};
			if (Settings.Destination is { Length: > 0 })
				payload["destination"] = Settings.Destination;

			await AuthorizedAsync(
				() => JsonRequest(HttpMethod.Post, Api("/torrents/add-urls"), payload),
				$"Flood {ClientName} add", cancellationToken);

			return ResolveTorrentId(release, magnetUrl, null);
		}

		var torrentData = await GetTorrentDataAsync(release, cancellationToken);

		await AuthorizedAsync(
			() => new HttpRequestMessage(HttpMethod.Post, Api("/torrents/add-files"))
			{
				Content = new MultipartFormDataContent
				{
					{ new ByteArrayContent(torrentData), "files", fileName },
					{ new StringContent(JsonSerializer.Serialize(tags)), "tags" }
				}
			}, $"Flood {ClientName} add", cancellationToken);

		return ResolveTorrentId(release, magnetUrl, torrentData);
	}

	/// <summary>Configured tags plus any per-release metadata tags enabled in <see cref="FloodSettings.AdditionalTags" /></summary>
	private List<string> BuildTags(RemoteRelease release)
	{
		var tags = new HashSet<string>(Settings.Tags);

		foreach (var additionalTag in Settings.AdditionalTags)
		{
			var value = additionalTag switch
			{
				FloodAdditionalTag.TITLE_SLUG => Slugify(release.Title),
				FloodAdditionalTag.QUALITY => release.Quality,
				FloodAdditionalTag.RELEASE_GROUP => release.ReleaseGroup,
				FloodAdditionalTag.YEAR => release.Year?.ToString(),
				FloodAdditionalTag.INDEXER => release.Indexer,
				FloodAdditionalTag.NETWORK => release.Network,
				FloodAdditionalTag.LANGUAGES => null, // added below, one tag per language
				_ => null
			};

			if (!string.IsNullOrWhiteSpace(value))
				tags.Add(value);

			if (additionalTag == FloodAdditionalTag.LANGUAGES)
				foreach (var language in release.Languages)
					if (!string.IsNullOrWhiteSpace(language))
						tags.Add(language);
		}

		return [.. tags];
	}

	private static string Slugify(string title)
		=> string.Concat(title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'))
			.Trim('-');

	/// <inheritdoc />
	protected override async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsyncCore(
		CancellationToken cancellationToken)
	{
		using var response = await AuthorizedAsync(
			() => new HttpRequestMessage(HttpMethod.Get, Api("/torrents")),
			$"Flood {ClientName} list", cancellationToken);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		return [.. document.RootElement.GetProperty("torrents").EnumerateObject()
			.Select(property => MapItem(property.Name, property.Value))];
	}

	/// <inheritdoc />
	protected override async Task RemoveAsyncCore(string downloadId, bool deleteData,
		CancellationToken cancellationToken)
		=> await AuthorizedAsync(
			() => JsonRequest(HttpMethod.Post, Api("/torrents/delete"),
				new { hashes = new[] { downloadId }, deleteData }),
			$"Flood {ClientName} remove", cancellationToken);

	/// <inheritdoc />
	public override async Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken)
	{
		if (Settings.PostImportTags.Count == 0)
			return;

		using var response = await AuthorizedAsync(
			() => new HttpRequestMessage(HttpMethod.Get, Api("/torrents")),
			$"Flood {ClientName} list", cancellationToken);

		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		if (!document.RootElement.GetProperty("torrents").TryGetProperty(downloadId, out var torrent))
			return;

		var tags = new HashSet<string>(Settings.PostImportTags);
		if (torrent.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
			tags.UnionWith(tagsElement.EnumerateArray().Select(tag => tag.GetString() ?? string.Empty));

		await AuthorizedAsync(
			() => new HttpRequestMessage(HttpMethod.Patch, Api("/torrents/tags"))
			{
				Content = JsonContent.Create(new { hashes = new[] { downloadId }, tags = tags.ToList() })
			}, $"Flood {ClientName} set tags", cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task TestAsyncCore(CancellationToken cancellationToken)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		using var response = await SendWithAuthAsync(
			() => new HttpRequestMessage(HttpMethod.Get, Api("/client/connection-test")), cancellationToken);

		if (response.StatusCode == HttpStatusCode.Unauthorized)
			throw new DownloadClientException("Flood authentication failed, check the username and password");

		EnsureSuccess(response, $"Flood {ClientName} connection test");
	}

	/// <summary>Sends a request, re-authenticating once on 401</summary>
	private async Task<HttpResponseMessage> AuthorizedAsync(Func<HttpRequestMessage> requestFactory,
		string operation, CancellationToken cancellationToken)
	{
		await EnsureAuthenticatedAsync(cancellationToken);

		var response = await SendWithAuthAsync(requestFactory, cancellationToken);

		if (response.StatusCode != HttpStatusCode.Unauthorized)
		{
			EnsureSuccess(response, operation);
			return response;
		}

		response.Dispose();
		_authenticated = false;
		await EnsureAuthenticatedAsync(cancellationToken);

		response = await SendWithAuthAsync(requestFactory, cancellationToken);
		EnsureSuccess(response, operation);

		return response;
	}

	private Task<HttpResponseMessage> SendWithAuthAsync(Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
		=> SendAsync(() =>
		{
			var request = requestFactory();
			if (_authCookie is not null)
				request.Headers.Add("Cookie", _authCookie);

			return request;
		}, cancellationToken);

	private static HttpRequestMessage JsonRequest(HttpMethod method, string url, object body)
		=> new(method, url) { Content = JsonContent.Create(body) };

	private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
	{
		if (_authenticated)
			return;

		using var response = await SendAsync(
			() => JsonRequest(HttpMethod.Post, Api("/auth/authenticate"),
				new { username = Settings.Username, password = Settings.Password }), cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new DownloadClientException("Flood authentication failed, check the username and password");

		if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
			_authCookie = cookies.FirstOrDefault()?.Split(';')[0];

		_authenticated = true;
	}

	private DownloadClientItem MapItem(string hash, JsonElement torrent)
	{
		var size = torrent.GetProperty("sizeBytes").GetInt64();
		var done = torrent.GetProperty("bytesDone").GetInt64();
		var eta = torrent.TryGetProperty("eta", out var etaElement) && etaElement.ValueKind == JsonValueKind.Number
			? etaElement.GetInt64()
			: 0;
		var tags = new List<string>();
		if (torrent.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
			tags.AddRange(tagsElement.EnumerateArray().Select(tag => tag.GetString() ?? string.Empty));

		return new DownloadClientItem
		{
			DownloadId = hash,
			Title = torrent.GetProperty("name").GetString() ?? string.Empty,
			TotalSize = size,
			RemainingSize = size - done,
			RemainingTime = eta > 0 ? TimeSpan.FromSeconds(eta) : null,
			Status = MapStatus(torrent.GetProperty("status")),
			OutputPath = torrent.TryGetProperty("directory", out var directory) ? directory.GetString() : null,
			Category = tags.FirstOrDefault(),
			IsReadOnly = Settings.Tags.Count > 0 && !tags.Any(tag => Settings.Tags.Contains(tag))
		};
	}

	private static DownloadItemStatus MapStatus(JsonElement status)
	{
		var states = status.EnumerateArray().Select(state => state.GetString()).ToHashSet();

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
}
