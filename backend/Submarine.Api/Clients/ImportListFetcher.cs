using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Core.ImportList;
using Submarine.Core.Library;

namespace Submarine.Api.Clients;

/// <summary>
///     Http implementation of <see cref="IImportListFetcher" /> for TMDB, Trakt and AniList sources
/// </summary>
public class ImportListFetcher : IImportListFetcher
{
	private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
	private const string TraktBaseUrl = "https://api.trakt.tv";
	private const string AniListGraphQlUrl = "https://graphql.anilist.co";

	private const string AniListSeasonQuery = """
		query ($season: MediaSeason, $seasonYear: Int) {
			Page(perPage: 50) {
				media(season: $season, seasonYear: $seasonYear, type: ANIME, sort: POPULARITY_DESC) {
					id
					idMal
					format
					title { romaji english }
				}
			}
		}
		""";

	private readonly HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private readonly ILogger<ImportListFetcher> _logger;

	/// <summary>
	///     Creates a new instance of <see cref="ImportListFetcher" />
	/// </summary>
	/// <param name="httpClient">Http client to fetch list sources with</param>
	/// <param name="configuration">configuration holding the source api keys</param>
	/// <param name="logger">logger of this fetcher</param>
	public ImportListFetcher(HttpClient httpClient, IConfiguration configuration, ILogger<ImportListFetcher> logger)
	{
		_httpClient = httpClient;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list,
		CancellationToken cancellationToken = default)
		=> list.Type switch
		{
			ImportListType.TMDB_LIST => FetchTmdbListAsync(list, cancellationToken),
			ImportListType.TMDB_POPULAR => FetchTmdbPopularAsync(list, cancellationToken),
			ImportListType.TRAKT_LIST => FetchTraktListAsync(list, cancellationToken),
			ImportListType.ANILIST_SEASON => FetchAniListSeasonAsync(cancellationToken),
			_ => Task.FromResult<IReadOnlyList<ImportListItem>>(Array.Empty<ImportListItem>())
		};

	private async Task<IReadOnlyList<ImportListItem>> FetchTmdbListAsync(ImportList list,
		CancellationToken cancellationToken)
	{
		var apiKey = _configuration.GetValue<string>("Tmdb:ApiKey");

		if (string.IsNullOrEmpty(apiKey))
		{
			_logger.LogWarning("Skipping import list {Name}, Tmdb:ApiKey is not configured", list.Name);
			return Array.Empty<ImportListItem>();
		}

		var listId = GetSetting(list, "listId");

		if (listId == null)
		{
			_logger.LogWarning("Skipping import list {Name}, settings have no listId", list.Name);
			return Array.Empty<ImportListItem>();
		}

		using var document = await GetJsonAsync(
			$"{TmdbBaseUrl}/list/{Uri.EscapeDataString(listId)}?api_key={Uri.EscapeDataString(apiKey)}",
			cancellationToken);

		var items = new List<ImportListItem>();

		foreach (var element in document.RootElement.GetProperty("items").EnumerateArray())
		{
			var isSeries = element.TryGetProperty("media_type", out var mediaType) &&
			               mediaType.GetString() == "tv";
			var title = GetString(element, isSeries ? "name" : "title");

			if (title == null)
				continue;

			items.Add(new ImportListItem(null, element.GetProperty("id").GetInt32(), null, title, isSeries));
		}

		return items;
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchTmdbPopularAsync(ImportList list,
		CancellationToken cancellationToken)
	{
		var apiKey = _configuration.GetValue<string>("Tmdb:ApiKey");

		if (string.IsNullOrEmpty(apiKey))
		{
			_logger.LogWarning("Skipping import list {Name}, Tmdb:ApiKey is not configured", list.Name);
			return Array.Empty<ImportListItem>();
		}

		var isSeries = list.MediaKind == MediaKind.SERIES;
		var kind = isSeries ? "tv" : "movie";

		using var document = await GetJsonAsync(
			$"{TmdbBaseUrl}/{kind}/popular?api_key={Uri.EscapeDataString(apiKey)}", cancellationToken);

		var items = new List<ImportListItem>();

		foreach (var element in document.RootElement.GetProperty("results").EnumerateArray())
		{
			var title = GetString(element, isSeries ? "name" : "title");

			if (title == null)
				continue;

			items.Add(new ImportListItem(null, element.GetProperty("id").GetInt32(), null, title, isSeries));
		}

		return items;
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchTraktListAsync(ImportList list,
		CancellationToken cancellationToken)
	{
		var clientId = _configuration.GetValue<string>("Trakt:ClientId");

		if (string.IsNullOrEmpty(clientId))
		{
			_logger.LogWarning("Skipping import list {Name}, Trakt:ClientId is not configured", list.Name);
			return Array.Empty<ImportListItem>();
		}

		var username = GetSetting(list, "username");
		var listId = GetSetting(list, "listId");

		if (username == null || listId == null)
		{
			_logger.LogWarning("Skipping import list {Name}, settings have no username/listId", list.Name);
			return Array.Empty<ImportListItem>();
		}

		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"{TraktBaseUrl}/users/{Uri.EscapeDataString(username)}/lists/{Uri.EscapeDataString(listId)}/items");
		request.Headers.Add("trakt-api-version", "2");
		request.Headers.Add("trakt-api-key", clientId);

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		using var document =
			await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
				cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();

		foreach (var element in document.RootElement.EnumerateArray())
		{
			var type = GetString(element, "type");

			if (type is not ("movie" or "show"))
				continue;

			var media = element.GetProperty(type);
			var title = GetString(media, "title");

			if (title == null)
				continue;

			var ids = media.GetProperty("ids");

			items.Add(new ImportListItem(GetInt(ids, "tvdb"), GetInt(ids, "tmdb"), null, title, type == "show"));
		}

		return items;
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchAniListSeasonAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var season = now.Month switch
		{
			<= 3 => "WINTER",
			<= 6 => "SPRING",
			<= 9 => "SUMMER",
			_ => "FALL"
		};

		using var response = await _httpClient.PostAsJsonAsync(AniListGraphQlUrl, new
		{
			query = AniListSeasonQuery,
			variables = new { season, seasonYear = now.Year }
		}, cancellationToken);

		response.EnsureSuccessStatusCode();

		using var document =
			await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
				cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();

		foreach (var element in document.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media")
			         .EnumerateArray())
		{
			var titleElement = element.GetProperty("title");
			var title = GetString(titleElement, "english") ?? GetString(titleElement, "romaji");

			if (title == null)
				continue;

			var isSeries = GetString(element, "format") != "MOVIE";

			items.Add(new ImportListItem(null, null, element.GetProperty("id").GetInt32(), title, isSeries));
		}

		return items;
	}

	private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync(url, cancellationToken);
		response.EnsureSuccessStatusCode();

		return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
			cancellationToken: cancellationToken);
	}

	private static string? GetSetting(ImportList list, string name)
	{
		if (string.IsNullOrEmpty(list.SettingsJson))
			return null;

		using var document = JsonDocument.Parse(list.SettingsJson);

		return GetString(document.RootElement, name);
	}

	private static string? GetString(JsonElement element, string name)
		=> element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static int? GetInt(JsonElement element, string name)
		=> element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: null;
}
