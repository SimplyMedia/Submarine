using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Options;

namespace Submarine.Metadata.Upstream;

/// <summary>
///     TMDB v3 access. Authenticates with a Bearer access token when
///     configured, otherwise with the v3 api_key query parameter.
/// </summary>
public sealed class TmdbClient(
	IHttpClientFactory httpClientFactory,
	IOptions<TmdbOptions> options,
	TimeProvider timeProvider,
	ILogger<TmdbClient> logger)
{
	public const string ClientName = "tmdb";
	public const int MaxFanOutConcurrency = 4;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
	{
		var detail = await GetJsonAsync<TmdbTvDetail>(
			$"tv/{tmdbId}?append_to_response=external_ids,alternative_titles,content_ratings,episode_groups", cancellationToken);
		if (detail is null) return null;

		var seasonNumbers = (detail.Seasons ?? [])
			.Select(s => s.SeasonNumber)
			.Where(n => n >= 0)
			.Distinct()
			.OrderBy(n => n);
		var seasons = await SelectAsync(
			seasonNumbers,
			MaxFanOutConcurrency,
			(season, token) => GetJsonAsync<TmdbSeason>($"tv/{tmdbId}/season/{season}", token),
			cancellationToken);

		var groups = detail.EpisodeGroups?.Results ?? [];
		var absoluteGroup = groups.FirstOrDefault(g => g.Type == 2);
		var dvdGroup = groups.FirstOrDefault(g => g.Type == 3);
		var absoluteDetail = absoluteGroup is null
			? null
			: await GetJsonAsync<TmdbEpisodeGroupDetail>($"tv/episode_group/{absoluteGroup.Id}", cancellationToken);
		var dvdDetail = dvdGroup is null
			? null
			: await GetJsonAsync<TmdbEpisodeGroupDetail>($"tv/episode_group/{dvdGroup.Id}", cancellationToken);

		return MapSeries(detail, seasons, absoluteDetail, dvdDetail);
	}

	public async Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
	{
		var detail = await GetJsonAsync<TmdbMovieDetail>(
			$"movie/{tmdbId}?append_to_response=release_dates,alternative_titles,videos,external_ids,keywords,credits", cancellationToken);
		return detail is null ? null : MapMovie(detail);
	}

	public async Task<MovieResource?> GetMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
	{
		var find = await GetJsonAsync<TmdbFindResults>(
			$"find/{Uri.EscapeDataString(imdbId)}?external_source=imdb_id", cancellationToken);
		var tmdbId = find?.MovieResults?.FirstOrDefault()?.Id;
		return tmdbId is null ? null : await GetMovieAsync(tmdbId.Value, cancellationToken);
	}

	public async Task<IReadOnlyList<SearchResultResource>> SearchMoviesAsync(string term, int? year, CancellationToken cancellationToken = default)
	{
		var query = $"search/movie?query={Uri.EscapeDataString(term)}";
		if (year is not null) query += $"&year={year}";
		var page = await GetJsonAsync<TmdbPage<TmdbMovieSummary>>(query, cancellationToken);
		return (page?.Results ?? []).Select(MapMovieSearchResult).ToList();
	}

	public async Task<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(string term, CancellationToken cancellationToken = default)
	{
		var page = await GetJsonAsync<TmdbPage<TmdbTvSummary>>($"search/tv?query={Uri.EscapeDataString(term)}", cancellationToken);
		return (page?.Results ?? []).Select(MapSeriesSearchResult).ToList();
	}

	public async Task<IReadOnlyList<SearchResultResource>> GetPopularMoviesAsync(int page, CancellationToken cancellationToken = default)
	{
		var result = await GetJsonAsync<TmdbPage<TmdbMovieSummary>>($"movie/popular?page={page}", cancellationToken);
		return (result?.Results ?? []).Select(MapMovieSearchResult).ToList();
	}

	public async Task<IReadOnlyList<SearchResultResource>> GetPopularSeriesAsync(int page, CancellationToken cancellationToken = default)
	{
		var result = await GetJsonAsync<TmdbPage<TmdbTvSummary>>($"tv/popular?page={page}", cancellationToken);
		return (result?.Results ?? []).Select(MapSeriesSearchResult).ToList();
	}

	public async Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default)
	{
		var detail = await GetJsonAsync<TmdbCollectionDetail>($"collection/{tmdbCollectionId}", cancellationToken);
		if (detail is null) return null;
		return new CollectionResource(
			detail.Id,
			detail.Name ?? string.Empty,
			detail.Overview,
			MetadataMapping.TmdbImage(detail.PosterPath),
			(detail.Parts ?? []).Select(MapMovieSummaryResource).ToList());
	}

	public async Task<IReadOnlyList<MovieResource>> GetMovieListAsync(int listId, CancellationToken cancellationToken = default)
	{
		var list = await GetJsonAsync<TmdbList>($"list/{listId}", cancellationToken);
		return (list?.Items ?? []).Select(MapMovieSummaryResource).ToList();
	}

	public async Task<IReadOnlyList<MovieResource>> GetMoviesByPersonAsync(int personId, CancellationToken cancellationToken = default)
	{
		var page = await GetJsonAsync<TmdbPage<TmdbMovieSummary>>(
			$"discover/movie?with_people={personId}&sort_by=popularity.desc", cancellationToken);
		return (page?.Results ?? []).Select(MapMovieSummaryResource).ToList();
	}

	internal MovieResource MapMovie(TmdbMovieDetail detail)
	{
		var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
		var releaseDates = detail.ReleaseDates?.Results.SelectMany(r => r.ReleaseDates).ToList() ?? [];
		var inCinemas = TypeDates(releaseDates, 3)
			.DefaultIfEmpty(MetadataMapping.ParseDate(detail.ReleaseDate))
			.MinOrNull();
		var digital = TypeDates(releaseDates, 4).MinOrNull();
		var physical = TypeDates(releaseDates, 5).MinOrNull();

		return new MovieResource(
			detail.Id,
			detail.ExternalIds?.ImdbId,
			detail.Title ?? string.Empty,
			detail.OriginalTitle,
			MetadataMapping.ToSortTitle(detail.Title),
			detail.Overview,
			inCinemas,
			digital,
			physical,
			MetadataMapping.DeriveMovieStatus(inCinemas, digital, physical, today),
			(inCinemas ?? digital ?? physical)?.Year,
			detail.Runtime,
			(detail.Genres ?? []).Select(g => g.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
			detail.ProductionCompanies?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Name))?.Name,
			releaseDates.Select(r => r.Certification).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
			MetadataMapping.TmdbImage(detail.PosterPath),
			MetadataMapping.TmdbImage(detail.BackdropPath),
			TrailerKey(detail.Videos),
			detail.BelongsToCollection?.Id,
			detail.BelongsToCollection?.Name,
			MapAlternateTitles(detail.AlternativeTitles, detail.Title, detail.OriginalTitle),
			detail.OriginalLanguage,
			(detail.Keywords?.Keywords ?? []).Select(k => k.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
			(detail.Credits?.Cast ?? []).Select(x => new MovieCreditResource(x.Id, x.Name ?? string.Empty, MetadataMapping.TmdbImage(x.ProfilePath), "Acting", null, x.Character, x.Order)).ToList()
				.Concat((detail.Credits?.Crew ?? []).Select(x => new MovieCreditResource(x.Id, x.Name ?? string.Empty, MetadataMapping.TmdbImage(x.ProfilePath), x.Department, x.Job, null, null))).ToList());
	}

	internal MovieResource MapMovieSummaryResource(TmdbMovieSummary summary)
	{
		var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
		var releaseDate = MetadataMapping.ParseDate(summary.ReleaseDate);
		return new MovieResource(
			summary.Id,
			null,
			summary.Title ?? string.Empty,
			summary.OriginalTitle,
			MetadataMapping.ToSortTitle(summary.Title),
			summary.Overview,
			releaseDate,
			null,
			null,
			MetadataMapping.DeriveMovieStatus(releaseDate, null, null, today),
			releaseDate?.Year,
			null,
			[],
			null,
			null,
			MetadataMapping.TmdbImage(summary.PosterPath),
			MetadataMapping.TmdbImage(summary.BackdropPath),
			null,
			null,
			null,
			[],
			summary.OriginalLanguage,
			[]);
	}

	internal static SeriesResource MapSeries(
		TmdbTvDetail detail,
		IReadOnlyList<TmdbSeason?> seasons,
		TmdbEpisodeGroupDetail? absoluteGroup,
		TmdbEpisodeGroupDetail? dvdGroup)
	{
		var episodes = seasons
			.Where(s => s is not null)
			.SelectMany(s => s!.Episodes ?? [])
			.ToList();

		var absoluteById = AbsoluteNumbers(absoluteGroup);
		var dvdById = DvdNumbers(dvdGroup);

		var episodeResources = episodes
			.OrderBy(e => e.SeasonNumber)
			.ThenBy(e => e.EpisodeNumber)
			.Select(e =>
			{
				var numbers = new List<EpisodeNumber>
				{
					new(EpisodeOrdering.AIRED, e.SeasonNumber, e.EpisodeNumber, null)
				};
				if (absoluteById.TryGetValue(e.Id, out var absoluteNumber))
					numbers.Add(new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, absoluteNumber));
				if (dvdById.TryGetValue(e.Id, out var dvdPosition))
					numbers.Add(new EpisodeNumber(EpisodeOrdering.DVD, dvdPosition.Season, dvdPosition.Number, null));
				return new EpisodeResource(
					null,
					e.Id,
					e.Name,
					e.Overview,
					MetadataMapping.ParseDate(e.AirDate),
					null,
					e.Runtime,
					numbers,
					MetadataMapping.TmdbImage(e.StillPath));
			})
			.ToList();

		var seasonResources = (detail.Seasons ?? [])
			.OrderBy(s => s.SeasonNumber)
			.Select(s => new SeasonResource(
				s.SeasonNumber,
				s.Name,
				s.EpisodeCount ?? episodes.Count(e => e.SeasonNumber == s.SeasonNumber)))
			.ToList();

		return new SeriesResource(
			detail.ExternalIds?.TvdbId ?? 0,
			detail.Id,
			detail.ExternalIds?.ImdbId,
			detail.Name ?? string.Empty,
			MetadataMapping.ToSortTitle(detail.Name),
			detail.Overview,
			MetadataMapping.ParseDate(detail.FirstAirDate),
			MetadataMapping.MapTmdbStatus(detail.Status),
			detail.EpisodeRunTime is { Count: > 0 } runtimes ? runtimes.Max() : null,
			detail.Networks?.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n.Name))?.Name,
			(detail.Genres ?? []).Select(g => g.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
			Certification(detail.ContentRatings),
			seasonResources,
			episodeResources,
			MetadataMapping.TmdbImage(detail.PosterPath),
			MetadataMapping.TmdbImage(detail.BackdropPath),
			MetadataMapping.ParseDate(detail.FirstAirDate)?.Year,
			MapAlternateTitles(detail.AlternativeTitles, detail.Name, detail.OriginalName),
			detail.OriginalLanguage);
	}

	internal static SearchResultResource MapSeriesSearchResult(TmdbTvSummary summary) => new(
		null,
		summary.Id,
		null,
		summary.Name ?? string.Empty,
		MetadataMapping.ParseDate(summary.FirstAirDate)?.Year,
		summary.Overview,
		MetadataMapping.TmdbImage(summary.PosterPath),
		null,
		"tmdb");

	internal static SearchResultResource MapMovieSearchResult(TmdbMovieSummary summary) => new(
		null,
		summary.Id,
		null,
		summary.Title ?? string.Empty,
		MetadataMapping.ParseDate(summary.ReleaseDate)?.Year,
		summary.Overview,
		MetadataMapping.TmdbImage(summary.PosterPath),
		null,
		"tmdb");

	private static IReadOnlyDictionary<int, int> AbsoluteNumbers(TmdbEpisodeGroupDetail? group)
	{
		if (group is null) return new Dictionary<int, int>();
		var absoluteById = new Dictionary<int, int>();
		var counter = 0;
		foreach (var entry in group.Groups.OrderBy(g => g.Order))
		foreach (var episode in entry.Episodes.OrderBy(e => e.Order))
			if (int.TryParse(episode.Id, out var id))
				absoluteById[id] = ++counter;
		return absoluteById;
	}

	private static IReadOnlyDictionary<int, (int Season, int Number)> DvdNumbers(TmdbEpisodeGroupDetail? group)
	{
		if (group is null) return new Dictionary<int, (int, int)>();
		var dvdById = new Dictionary<int, (int, int)>();
		foreach (var entry in group.Groups.OrderBy(g => g.Order))
		foreach (var episode in entry.Episodes.OrderBy(e => e.Order))
			if (int.TryParse(episode.Id, out var id))
				dvdById[id] = (entry.Order, (episode.Order ?? 0) + 1);
		return dvdById;
	}

	private static string? Certification(TmdbContentRatings? ratings)
	{
		var results = ratings?.Results ?? [];
		return results.FirstOrDefault(r => r.Iso31661 == "US")?.Rating
			?? results.FirstOrDefault()?.Rating;
	}

	private static string? TrailerKey(TmdbVideos? videos)
	{
		var trailers = (videos?.Results ?? [])
			.Where(v => v.Site?.Equals("YouTube", StringComparison.OrdinalIgnoreCase) == true
				&& v.Type?.Equals("Trailer", StringComparison.OrdinalIgnoreCase) == true
				&& !string.IsNullOrWhiteSpace(v.Key))
			.ToList();
		return (trailers.FirstOrDefault(v => v.Official == true) ?? trailers.FirstOrDefault())?.Key;
	}

	private static IReadOnlyList<AlternateTitleResource> MapAlternateTitles(
		TmdbAlternativeTitles? alternativeTitles,
		string? title,
		string? originalTitle)
		=> (alternativeTitles?.Results ?? [])
			.Where(t => !string.IsNullOrWhiteSpace(t.Title)
				&& !t.Title!.Equals(title, StringComparison.OrdinalIgnoreCase)
				&& !t.Title!.Equals(originalTitle, StringComparison.OrdinalIgnoreCase))
			.Select(t => new AlternateTitleResource(t.Title!.Trim(), t.Iso31661))
			.Distinct()
			.ToList();

	private static IEnumerable<DateOnly?> TypeDates(IEnumerable<TmdbReleaseDate> releaseDates, int type) =>
		releaseDates
			.Where(r => r.Type == type)
			.Select(r => MetadataMapping.ParseDate(r.ReleaseDate));

	private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken) where T : class
	{
		var tmdbOptions = options.Value;
		// Bearer authentication is set on the named client; without an access
		// token fall back to the v3 api_key query parameter.
		if (string.IsNullOrEmpty(tmdbOptions.AccessToken) && !string.IsNullOrEmpty(tmdbOptions.ApiKey))
			path += $"{(path.Contains('?') ? '&' : '?')}api_key={Uri.EscapeDataString(tmdbOptions.ApiKey)}";
		var client = httpClientFactory.CreateClient(ClientName);
		using var response = await client.GetAsync(path, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound) return null;
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning(
				"TMDB request to '{Path}' failed with status {Status}.",
				UpstreamPath.WithoutQuery(path),
				(int)response.StatusCode);
			throw new UpstreamException(
				"tmdb",
				(int)response.StatusCode,
				$"TMDB request failed with status {(int)response.StatusCode}.");
		}

		return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
	}

	private static async Task<IReadOnlyList<T>> SelectAsync<TSource, T>(
		IEnumerable<TSource> source,
		int maxConcurrency,
		Func<TSource, CancellationToken, Task<T?>> selector,
		CancellationToken cancellationToken)
	{
		using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
		var tasks = source.Select(async item =>
		{
			await gate.WaitAsync(cancellationToken);
			try
			{
				return await selector(item, cancellationToken);
			}
			finally
			{
				gate.Release();
			}
		});
		var results = await Task.WhenAll(tasks);
		return results.Where(r => r is not null).Cast<T>().ToList();
	}
}
