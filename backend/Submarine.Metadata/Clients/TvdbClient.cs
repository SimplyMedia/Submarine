using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Metadata.Contracts;
using Submarine.Metadata.Services;

namespace Submarine.Metadata.Clients;

/// <summary>
///     Typed client for the TVDB v4 API, normalizing responses into <see cref="SeriesResource" />
/// </summary>
public class TvdbClient
{
	private const string TokenCacheKey = "Tvdb:Token";

	private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(28);

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly HttpClient _httpClient;
	private readonly IMemoryCache _cache;
	private readonly string _apiKey;

	/// <summary>
	///     Creates a new instance of <see cref="TvdbClient" />
	/// </summary>
	/// <param name="httpClient">http client configured with the TVDB base address</param>
	/// <param name="cache">cache to store the TVDB bearer token in</param>
	/// <param name="configuration">configuration to read the TVDB api key from</param>
	public TvdbClient(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
	{
		_httpClient = httpClient;
		_cache = cache;
		_apiKey = configuration["Tvdb:ApiKey"] ?? string.Empty;
	}

	/// <summary>
	///     Searches TVDB for series matching the given search term
	/// </summary>
	/// <param name="term">search term</param>
	/// <returns>normalized search results, without seasons or episodes</returns>
	public async Task<IReadOnlyList<SeriesResource>> SearchSeriesAsync(string term)
	{
		var results = await GetAsync<IReadOnlyList<TvdbSearchResult>>(
			$"search?query={Uri.EscapeDataString(term)}&type=series");

		return results?.Select(MapSearchResult).ToList() ?? [];
	}

	/// <summary>
	///     Gets a single series from TVDB by its identifier, including every episode with all available orderings
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <returns>normalized series, or null if it could not be found</returns>
	public async Task<SeriesResource?> GetSeriesAsync(int tvdbId)
	{
		var series = await GetAsync<TvdbSeriesExtended>($"series/{tvdbId}/extended?meta=episodes");

		return series == null ? null : MapSeries(series);
	}

	private async Task<T?> GetAsync<T>(string uri)
	{
		var response = await SendAsync(uri);

		if (response.StatusCode == HttpStatusCode.Unauthorized)
		{
			_cache.Remove(TokenCacheKey);
			response = await SendAsync(uri);
		}

		response.EnsureSuccessStatusCode();

		var envelope = await response.Content.ReadFromJsonAsync<TvdbResponse<T>>(JsonOptions);

		return envelope == null ? default : envelope.Data;
	}

	private async Task<HttpResponseMessage> SendAsync(string uri)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, uri);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());

		return await _httpClient.SendAsync(request);
	}

	private async Task<string> GetTokenAsync()
		=> (await _cache.GetOrCreateAsync(TokenCacheKey, async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = TokenTtl;

			var response = await _httpClient.PostAsJsonAsync("login", new { apikey = _apiKey }, JsonOptions);
			response.EnsureSuccessStatusCode();

			var login = await response.Content.ReadFromJsonAsync<TvdbResponse<TvdbLoginData>>(JsonOptions);

			return login?.Data?.Token ?? throw new InvalidOperationException("TVDB login did not return a token");
		}))!;

	private static SeriesResource MapSeries(TvdbSeriesExtended series)
	{
		var firstAired = ParseDate(series.FirstAired);
		var rawEpisodes = series.Episodes ?? [];

		var seasonNames = (series.Seasons ?? [])
			.Where(s => s.Type?.Type == "official")
			.GroupBy(s => s.Number)
			.ToDictionary(g => g.Key, g => g.First().Name);

		var seasons = rawEpisodes
			.GroupBy(e => e.SeasonNumber)
			.OrderBy(g => g.Key)
			.Select(g => new SeasonResource(g.Key, seasonNames.GetValueOrDefault(g.Key), g.Count()))
			.ToList();

		return new SeriesResource(
			series.Id,
			null,
			series.Name,
			null,
			series.Overview,
			firstAired,
			ParseStatus(series.Status?.Name),
			series.AverageRuntime,
			series.OriginalNetwork?.Name,
			series.Genres?.Select(g => g.Name).ToList() ?? [],
			seasons,
			EpisodeOrderingService.Normalize(rawEpisodes),
			series.Image,
			firstAired?.Year);
	}

	private static SeriesResource MapSearchResult(TvdbSearchResult result)
	{
		var firstAired = ParseDate(result.FirstAirTime);

		return new SeriesResource(
			int.Parse(result.TvdbId),
			null,
			result.Name,
			null,
			result.Overview,
			firstAired,
			ParseStatus(result.Status),
			null,
			result.Network,
			[],
			[],
			[],
			result.ImageUrl,
			int.TryParse(result.Year, out var year) ? year : firstAired?.Year);
	}

	private static SeriesStatus ParseStatus(string? status)
		=> Enum.TryParse<SeriesStatus>(status, true, out var parsed) ? parsed : SeriesStatus.Unknown;

	private static DateOnly? ParseDate(string? date)
		=> DateOnly.TryParse(date, out var parsed) ? parsed : null;
}
