using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Clients;

/// <summary>
///     Typed client for the TMDB API, normalizing responses into <see cref="MovieResource" /> and
///     <see cref="SeriesResource" />
/// </summary>
public class TmdbClient
{
	private const string ImageBaseUrl = "https://image.tmdb.org/t/p/original";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly HttpClient _httpClient;
	private readonly string _apiKey;

	/// <summary>
	///     Creates a new instance of <see cref="TmdbClient" />
	/// </summary>
	/// <param name="httpClient">http client configured with the TMDB base address</param>
	/// <param name="configuration">configuration to read the TMDB api key from</param>
	public TmdbClient(HttpClient httpClient, IConfiguration configuration)
	{
		_httpClient = httpClient;
		_apiKey = configuration["Tmdb:ApiKey"] ?? string.Empty;
	}

	/// <summary>
	///     Searches TMDB for movies matching the given search term
	/// </summary>
	/// <param name="term">search term</param>
	/// <returns>normalized search results</returns>
	public async Task<IReadOnlyList<MovieResource>> SearchMoviesAsync(string term)
	{
		var response = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(
			$"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(term)}", JsonOptions);

		return response?.Results.Select(MapMovie).ToList() ?? [];
	}

	/// <summary>
	///     Gets a single movie from TMDB by its identifier
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the movie</param>
	/// <returns>normalized movie, or null if it could not be found</returns>
	public async Task<MovieResource?> GetMovieAsync(int tmdbId)
	{
		var movie = await _httpClient.GetFromJsonAsync<TmdbMovie>($"movie/{tmdbId}?api_key={_apiKey}", JsonOptions);

		return movie == null ? null : MapMovie(movie);
	}

	/// <summary>
	///     Gets a single movie collection from TMDB by its identifier
	/// </summary>
	/// <param name="collectionId">TheMovieDB identifier of the collection</param>
	/// <returns>normalized collection, or null if it could not be found</returns>
	public async Task<CollectionResource?> GetCollectionAsync(int collectionId)
	{
		var collection = await _httpClient.GetFromJsonAsync<TmdbCollection>(
			$"collection/{collectionId}?api_key={_apiKey}", JsonOptions);

		if (collection == null)
			return null;

		var movies = (collection.Parts ?? [])
			.Select(p => MapCollectionPart(p, collection.Id, collection.Name))
			.ToList();

		return new CollectionResource(collection.Id, collection.Name, collection.Overview, movies);
	}

	/// <summary>
	///     Searches TMDB for series matching the given search term
	/// </summary>
	/// <param name="term">search term</param>
	/// <returns>normalized search results, without seasons or episodes</returns>
	public async Task<IReadOnlyList<SeriesResource>> SearchSeriesAsync(string term)
	{
		var response = await _httpClient.GetFromJsonAsync<TmdbTvSearchResponse>(
			$"search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(term)}", JsonOptions);

		return response?.Results.Select(MapSearchResult).ToList() ?? [];
	}

	/// <summary>
	///     Gets a single series from TMDB by its identifier, fanning out per-season episode fetches (specials included as
	///     season 0). Every episode carries an aired <see cref="EpisodeNumber" />; dvd and absolute orderings are merged
	///     in from TMDB episode groups when available.
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the series</param>
	/// <returns>normalized series, or null if it could not be found</returns>
	public async Task<SeriesResource?> GetSeriesAsync(int tmdbId)
	{
		var series = await _httpClient.GetFromJsonAsync<TmdbSeries>(
			$"tv/{tmdbId}?api_key={_apiKey}&append_to_response=external_ids", JsonOptions);

		if (series == null)
			return null;

		var seasonDetails = await Task.WhenAll((series.Seasons ?? [])
			.Select(s => GetSeasonAsync(tmdbId, s.SeasonNumber)));

		var episodes = seasonDetails
			.SelectMany(d => d?.Episodes ?? [])
			.Select(MapEpisode)
			.ToList();

		var enriched = await ApplyEpisodeGroupsAsync(tmdbId, episodes);

		return MapSeries(series, enriched);
	}

	private Task<TmdbSeasonDetail?> GetSeasonAsync(int tmdbId, int seasonNumber)
		=> _httpClient.GetFromJsonAsync<TmdbSeasonDetail>(
			$"tv/{tmdbId}/season/{seasonNumber}?api_key={_apiKey}", JsonOptions);

	// TMDB episode group type ids: 2 = absolute ordering, 3 = dvd ordering
	private async Task<IReadOnlyList<EpisodeResource>> ApplyEpisodeGroupsAsync(int tmdbId,
		IReadOnlyList<EpisodeResource> episodes)
	{
		var groups = await _httpClient.GetFromJsonAsync<TmdbEpisodeGroupsResponse>(
			$"tv/{tmdbId}/episode_groups?api_key={_apiKey}", JsonOptions);

		var results = groups?.Results ?? [];
		var dvdGroup = results.FirstOrDefault(g => g.Type == 3);
		var absoluteGroup = results.FirstOrDefault(g => g.Type == 2);

		if (dvdGroup == null && absoluteGroup == null)
			return episodes;

		var dvdNumbers = dvdGroup == null ? null : await BuildDvdNumbersAsync(dvdGroup.Id);
		var absoluteNumbers = absoluteGroup == null ? null : await BuildAbsoluteNumbersAsync(absoluteGroup.Id);

		return episodes.Select(e => MergeOrderings(e, dvdNumbers, absoluteNumbers)).ToList();
	}

	private async Task<IReadOnlyDictionary<int, EpisodeNumber>> BuildDvdNumbersAsync(string groupId)
	{
		var detail = await GetEpisodeGroupAsync(groupId);
		var numbers = new Dictionary<int, EpisodeNumber>();

		foreach (var season in (detail?.Groups ?? []).OrderBy(g => g.Order))
			foreach (var episode in (season.Episodes ?? []).OrderBy(e => e.Order))
				numbers[episode.Id] = new EpisodeNumber(EpisodeOrdering.Dvd, season.Order, episode.Order + 1, null);

		return numbers;
	}

	private async Task<IReadOnlyDictionary<int, EpisodeNumber>> BuildAbsoluteNumbersAsync(string groupId)
	{
		var detail = await GetEpisodeGroupAsync(groupId);
		var numbers = new Dictionary<int, EpisodeNumber>();
		var running = 0;

		foreach (var season in (detail?.Groups ?? []).OrderBy(g => g.Order))
			foreach (var episode in (season.Episodes ?? []).OrderBy(e => e.Order))
				numbers[episode.Id] = new EpisodeNumber(EpisodeOrdering.Absolute, null, null, ++running);

		return numbers;
	}

	private Task<TmdbEpisodeGroupDetail?> GetEpisodeGroupAsync(string groupId)
		=> _httpClient.GetFromJsonAsync<TmdbEpisodeGroupDetail>(
			$"tv/episode_group/{groupId}?api_key={_apiKey}", JsonOptions);

	private static EpisodeResource MergeOrderings(EpisodeResource episode,
		IReadOnlyDictionary<int, EpisodeNumber>? dvdNumbers, IReadOnlyDictionary<int, EpisodeNumber>? absoluteNumbers)
	{
		if (episode.TmdbId == null)
			return episode;

		var dvd = dvdNumbers != null && dvdNumbers.TryGetValue(episode.TmdbId.Value, out var d) ? d : null;
		var absolute = absoluteNumbers != null && absoluteNumbers.TryGetValue(episode.TmdbId.Value, out var a)
			? a
			: null;

		if (dvd == null && absolute == null)
			return episode;

		var numbers = episode.Numbers.ToList();

		if (dvd != null)
			numbers.Add(dvd);

		if (absolute != null)
			numbers.Add(absolute);

		return episode with { Numbers = numbers };
	}

	private static SeriesResource MapSeries(TmdbSeries series, IReadOnlyList<EpisodeResource> episodes)
	{
		var firstAired = ParseDate(series.FirstAirDate);

		var seasons = (series.Seasons ?? [])
			.OrderBy(s => s.SeasonNumber)
			.Select(s => new SeasonResource(s.SeasonNumber, s.Name, s.EpisodeCount))
			.ToList();

		int? runtime = series.EpisodeRunTime is { Count: > 0 } runtimes ? runtimes[0] : null;

		return new SeriesResource(
			series.ExternalIds?.TvdbId ?? 0,
			series.Id,
			series.Name,
			null,
			series.Overview,
			firstAired,
			MapStatus(series.Status),
			runtime,
			series.Networks?.FirstOrDefault()?.Name,
			series.Genres?.Select(g => g.Name).ToList() ?? [],
			seasons,
			episodes,
			series.PosterPath == null ? null : ImageBaseUrl + series.PosterPath,
			firstAired?.Year,
			series.BackdropPath == null ? null : ImageBaseUrl + series.BackdropPath);
	}

	private static SeriesResource MapSearchResult(TmdbTvSearchResult result)
	{
		var firstAired = ParseDate(result.FirstAirDate);

		return new SeriesResource(
			0,
			result.Id,
			result.Name,
			null,
			result.Overview,
			firstAired,
			SeriesStatus.Unknown,
			null,
			null,
			[],
			[],
			[],
			result.PosterPath == null ? null : ImageBaseUrl + result.PosterPath,
			firstAired?.Year);
	}

	private static EpisodeResource MapEpisode(TmdbEpisode episode)
		=> new(
			null,
			episode.Id,
			episode.Name,
			episode.Overview,
			ParseDate(episode.AirDate),
			episode.Runtime,
			[new EpisodeNumber(EpisodeOrdering.Aired, episode.SeasonNumber, episode.EpisodeNumber, null)]);

	private static SeriesStatus MapStatus(string? status)
		=> status switch
		{
			"Returning Series" => SeriesStatus.Continuing,
			"Ended" or "Canceled" => SeriesStatus.Ended,
			"Planned" or "In Production" or "Pilot" => SeriesStatus.Upcoming,
			_ => SeriesStatus.Unknown
		};

	private static MovieResource MapMovie(TmdbMovie movie)
	{
		var releaseDate = ParseDate(movie.ReleaseDate);

		return new MovieResource(
			movie.Id,
			movie.ImdbId,
			movie.Title,
			null,
			movie.Overview,
			releaseDate,
			releaseDate?.Year,
			movie.Runtime,
			movie.Genres?.Select(g => g.Name).ToList() ?? [],
			movie.ProductionCompanies?.FirstOrDefault()?.Name,
			movie.PosterPath == null ? null : ImageBaseUrl + movie.PosterPath,
			[],
			movie.BelongsToCollection?.Id,
			movie.BelongsToCollection?.Name,
			movie.BackdropPath == null ? null : ImageBaseUrl + movie.BackdropPath);
	}

	private static MovieResource MapCollectionPart(TmdbCollectionPart part, int collectionId, string collectionTitle)
	{
		var releaseDate = ParseDate(part.ReleaseDate);

		return new MovieResource(
			part.Id,
			null,
			part.Title,
			null,
			part.Overview,
			releaseDate,
			releaseDate?.Year,
			null,
			[],
			null,
			part.PosterPath == null ? null : ImageBaseUrl + part.PosterPath,
			[],
			collectionId,
			collectionTitle);
	}

	private static DateOnly? ParseDate(string? date)
		=> DateOnly.TryParse(date, out var parsed) ? parsed : null;
}
