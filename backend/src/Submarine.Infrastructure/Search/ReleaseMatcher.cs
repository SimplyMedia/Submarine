using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Release;
using Submarine.Core.Search;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     A release matched to a library item.
/// </summary>
/// <param name="SeriesId">Id of the matched series, when the release matched a series.</param>
/// <param name="MovieId">Id of the matched movie, when the release matched a movie.</param>
/// <param name="EpisodeIds">Ids of the matched episodes, when the release matched a series.</param>
public sealed record ReleaseMatch(int? SeriesId, int? MovieId, IReadOnlyList<int> EpisodeIds);

/// <summary>
///     Matches a parsed release to the library, resolving numbering against the matched series' aired and stored
///     scene numbers.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="mappingsClient">Used to resolve a release title to a series by its known scene names.</param>
public sealed partial class ReleaseMatcher(SubmarineDbContext db, IMappingsClient mappingsClient)
{
	/// <summary>
	///     Matches the release against a specific, already known series.
	/// </summary>
	public async Task<ReleaseMatch?> MatchSeriesAsync(Series series, BaseRelease release, CancellationToken cancellationToken = default)
	{
		var episodes = await db.Episodes.AsNoTracking().Where(episode => episode.SeriesId == series.Id).ToListAsync(cancellationToken);
		var dailyAirDate = series.Type == SeriesType.DAILY ? ExtractDailyDate(release.FullTitle) : null;
		var episodeIds = EpisodeSelector.Select(release.SeriesReleaseData, episodes, dailyAirDate);
		return episodeIds.Count == 0 ? null : new ReleaseMatch(series.Id, null, episodeIds);
	}

	/// <summary>
	///     Matches the release against a specific, already known movie. Movies always match; there is no numbering to
	///     resolve. Used for interactive search, where the user already picked the release from a visible result list.
	/// </summary>
	public static ReleaseMatch MatchMovie(Movie movie)
		=> new(null, movie.Id, []);

	/// <summary>
	///     Validates a parsed release against a specific movie for an automatic search grab, where nothing else confirms
	///     the result: matches by TMDB or IMDB id when the indexer reported one, otherwise by clean title and year
	///     within one year of tolerance.
	/// </summary>
	public async Task<bool> MatchesMovieAsync(Movie movie, ReleaseInfo info, BaseRelease release, CancellationToken cancellationToken = default)
	{
		if (info.TmdbId is { } tmdbId)
		{
			return tmdbId == movie.TmdbId;
		}

		if (info.ImdbId is { } imdbId && movie.ImdbId is not null)
		{
			return string.Equals(imdbId, movie.ImdbId, StringComparison.OrdinalIgnoreCase);
		}

		var candidates = await LoadMovieTitlesAsync(cancellationToken);
		var titles = candidates.FirstOrDefault(entry => entry.Id == movie.Id)?.Titles ?? [movie.Title];
		return TitleMatcher.IsMatch(release.Title, release.Year, titles, movie.Year);
	}

	/// <summary>
	///     Matches the release against the whole library by title, used for RSS sync and free text search where the
	///     target series or movie is not known ahead of time.
	/// </summary>
	public async Task<ReleaseMatch?> MatchLibraryAsync(BaseRelease release, CancellationToken cancellationToken = default)
	{
		if (release.SeriesReleaseData is not null)
		{
			var series = await FindSeriesAsync(release, cancellationToken);
			return series is null ? null : await MatchSeriesAsync(series, release, cancellationToken);
		}

		var movie = await FindMovieAsync(release, cancellationToken);
		return movie is null ? null : MatchMovie(movie);
	}

	private async Task<Series?> FindSeriesAsync(BaseRelease release, CancellationToken cancellationToken)
	{
		var candidates = await LoadSeriesTitlesAsync(cancellationToken);

		foreach (var candidate in candidates)
		{
			if (TitleMatcher.IsMatch(release.Title, release.Year, candidate.Titles, candidate.Year))
			{
				return await db.Series.AsNoTracking().FirstAsync(series => series.Id == candidate.Id, cancellationToken);
			}
		}

		var tvdbIds = await mappingsClient.FindByNameAsync(release.Title, cancellationToken);
		return tvdbIds.Count == 0
			? null
			: await db.Series.AsNoTracking().FirstOrDefaultAsync(series => tvdbIds.Contains(series.TvdbId), cancellationToken);
	}

	private async Task<Movie?> FindMovieAsync(BaseRelease release, CancellationToken cancellationToken)
	{
		var candidates = await LoadMovieTitlesAsync(cancellationToken);

		foreach (var candidate in candidates)
		{
			if (TitleMatcher.IsMatch(release.Title, release.Year, candidate.Titles, candidate.Year))
			{
				return await db.Movies.AsNoTracking().FirstAsync(movie => movie.Id == candidate.Id, cancellationToken);
			}
		}

		return null;
	}

	// preloaded once per ReleaseMatcher instance (one search/RSS batch, since it is scoped per command/request) rather
	// than querying alternative titles per candidate series/movie for every release
	private List<TitleCandidate>? _seriesTitles;
	private List<TitleCandidate>? _movieTitles;

	private async Task<List<TitleCandidate>> LoadSeriesTitlesAsync(CancellationToken cancellationToken)
	{
		if (_seriesTitles is not null)
		{
			return _seriesTitles;
		}

		var series = await db.Series.AsNoTracking().Select(entity => new { entity.Id, entity.Title, entity.Year }).ToListAsync(cancellationToken);
		var alternatives = await db.AlternativeTitles.AsNoTracking().Where(alternative => alternative.SeriesId != null)
			.Select(alternative => new { SeriesId = alternative.SeriesId!.Value, alternative.Title }).ToListAsync(cancellationToken);
		var lookup = alternatives.ToLookup(alternative => alternative.SeriesId, alternative => alternative.Title);

		_seriesTitles = [.. series.Select(entity => new TitleCandidate(entity.Id, [entity.Title, .. lookup[entity.Id]], entity.Year))];
		return _seriesTitles;
	}

	private async Task<List<TitleCandidate>> LoadMovieTitlesAsync(CancellationToken cancellationToken)
	{
		if (_movieTitles is not null)
		{
			return _movieTitles;
		}

		var movies = await db.Movies.AsNoTracking().Select(entity => new { entity.Id, entity.Title, entity.Year }).ToListAsync(cancellationToken);
		var alternatives = await db.AlternativeTitles.AsNoTracking().Where(alternative => alternative.MovieId != null)
			.Select(alternative => new { MovieId = alternative.MovieId!.Value, alternative.Title }).ToListAsync(cancellationToken);
		var lookup = alternatives.ToLookup(alternative => alternative.MovieId, alternative => alternative.Title);

		_movieTitles = [.. movies.Select(entity => new TitleCandidate(entity.Id, [entity.Title, .. lookup[entity.Id]], entity.Year))];
		return _movieTitles;
	}

	private sealed record TitleCandidate(int Id, IReadOnlyList<string> Titles, int? Year);

	private static DateTime? ExtractDailyDate(string fullTitle)
	{
		var match = DailyDatePattern().Match(fullTitle);
		return match.Success
			? new DateTime(
				int.Parse(match.Groups["y"].Value),
				int.Parse(match.Groups["m"].Value),
				int.Parse(match.Groups["d"].Value),
				0, 0, 0, DateTimeKind.Utc)
			: null;
	}

	[GeneratedRegex(@"(?<y>(19|20)\d{2})[.\-](?<m>\d{2})[.\-](?<d>\d{2})")]
	private static partial Regex DailyDatePattern();
}
