using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Search;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Runs automatic (command driven) searches: searches, matches, decides and grabs the best approved release per
///     media version, holding temporary-only rejections as pending releases.
/// </summary>
public sealed class AutomaticSearchService(
	SubmarineDbContext db,
	ReleaseSearchService searchService,
	ReleaseMatcher matcher,
	DecisionContextFactory contextFactory,
	IDownloadDecisionMaker decisionMaker,
	IGrabService grabService,
	EpisodeSearchPlanner episodePlanner,
	IParser<TorrentRelease> torrentParser,
	IParser<UsenetRelease> usenetParser,
	TimeProvider timeProvider,
	ILogger<AutomaticSearchService> logger)
{
	/// <summary>Searches for every episode of a series.</summary>
	public async Task SearchSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
	{
		var series = await db.Series.FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");
		var request = SearchRequestBuilder.BuildSeriesQuery(series.Title, series.TvdbId, series.TmdbId, series.ImdbId);
		var releases = await searchService.SearchAsync(request, IndexerSearchMode.AUTOMATIC, "series-search", cancellationToken);
		await ProcessSeriesReleasesAsync(series, releases, cancellationToken);
	}

	/// <summary>Searches for one season of a series.</summary>
	public async Task SearchSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
	{
		var series = await db.Series.FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");
		var request = SearchRequestBuilder.BuildSeasonQuery(series.Title, seasonNumber, series.TvdbId, series.TmdbId, series.ImdbId);
		var releases = await searchService.SearchAsync(request, IndexerSearchMode.AUTOMATIC, "season-search", cancellationToken);
		await ProcessSeriesReleasesAsync(series, releases, cancellationToken);
	}

	/// <summary>Searches for a specific set of episodes, one search per episode.</summary>
	public async Task SearchEpisodesAsync(IReadOnlyList<int> episodeIds, CancellationToken cancellationToken = default)
	{
		var episodes = await db.Episodes.Where(episode => episodeIds.Contains(episode.Id)).ToListAsync(cancellationToken);
		var seriesCache = new Dictionary<int, Series>();

		foreach (var episode in episodes)
		{
			if (!seriesCache.TryGetValue(episode.SeriesId, out var series))
			{
				var loaded = await db.Series.FirstOrDefaultAsync(candidate => candidate.Id == episode.SeriesId, cancellationToken);
				if (loaded is null)
				{
					continue;
				}

				series = loaded;
				seriesCache[episode.SeriesId] = series;
			}

			var requests = await episodePlanner.BuildAsync(series, episode, cancellationToken);
			var releases = new List<ReleaseInfo>();
			foreach (var request in requests)
			{
				releases.AddRange(await searchService.SearchAsync(request, IndexerSearchMode.AUTOMATIC, "episode-search", cancellationToken));
			}

			await ProcessSeriesReleasesAsync(series, releases, cancellationToken);
			episode.LastSearchTime = timeProvider.GetUtcNow().UtcDateTime;
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	/// <summary>Searches for a specific set of movies.</summary>
	public async Task SearchMoviesAsync(IReadOnlyList<int> movieIds, CancellationToken cancellationToken = default)
	{
		var movies = await db.Movies.Where(movie => movieIds.Contains(movie.Id)).ToListAsync(cancellationToken);

		foreach (var movie in movies)
		{
			var request = SearchRequestBuilder.BuildMovieQuery(movie.Title, movie.Year, movie.ImdbId, movie.TmdbId);
			var releases = await searchService.SearchAsync(request, IndexerSearchMode.AUTOMATIC, "movie-search", cancellationToken);
			await ProcessMovieReleasesAsync(movie, releases, cancellationToken);
			movie.LastSearchTime = timeProvider.GetUtcNow().UtcDateTime;
		}

		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task ProcessSeriesReleasesAsync(Series series, IReadOnlyList<ReleaseInfo> releases, CancellationToken cancellationToken)
	{
		var candidates = new List<ReleaseCandidate>();

		foreach (var info in releases)
		{
			var parsed = TryParse(info);
			if (parsed is null)
			{
				continue;
			}

			var match = await matcher.MatchSeriesAsync(series, parsed, cancellationToken);
			if (match is null)
			{
				continue;
			}

			candidates.Add(new ReleaseCandidate(parsed, info, null, match.SeriesId, match.MovieId, match.EpisodeIds));
		}

		await ProcessSeriesCandidatesAsync(series.Id, candidates, cancellationToken);
	}

	/// <summary>
	///     Decides and grabs the best approved release per media version and matched episode set, for candidates already
	///     matched to the given series. Used directly by RSS sync, which matches releases across the whole library up
	///     front.
	/// </summary>
	public async Task ProcessSeriesCandidatesAsync(int seriesId, IReadOnlyList<ReleaseCandidate> candidates, CancellationToken cancellationToken = default)
	{
		if (candidates.Count == 0)
		{
			return;
		}

		var versions = await db.MediaVersions.Where(version => version.SeriesId == seriesId).ToListAsync(cancellationToken);

		foreach (var version in versions)
		{
			var grabbedEpisodeIds = new HashSet<int>();

			// season packs first: once one covers a set of episodes, skip singles/smaller packs for the same episodes
			var groups = candidates
				.GroupBy(candidate => string.Join(",", candidate.EpisodeIds ?? []))
				.Where(group => group.Key.Length > 0)
				.OrderByDescending(group => group.First().EpisodeIds!.Count);

			foreach (var group in groups)
			{
				var episodeIds = group.First().EpisodeIds!;
				if (episodeIds.Any(grabbedEpisodeIds.Contains))
				{
					continue;
				}

				var context = await contextFactory.BuildAsync(version, episodeIds, cancellationToken);
				var decisions = decisionMaker.DecideAll([.. group], context);
				if (await GrabBestAsync(decisions, version, seriesId, null, cancellationToken))
				{
					grabbedEpisodeIds.UnionWith(episodeIds);
				}
			}
		}
	}

	private async Task ProcessMovieReleasesAsync(Movie movie, IReadOnlyList<ReleaseInfo> releases, CancellationToken cancellationToken)
	{
		var candidates = new List<ReleaseCandidate>();

		foreach (var info in releases)
		{
			var parsed = TryParse(info);
			if (parsed is null)
			{
				continue;
			}

			if (!await matcher.MatchesMovieAsync(movie, info, parsed, cancellationToken))
			{
				continue;
			}

			candidates.Add(new ReleaseCandidate(parsed, info, null, null, movie.Id, []));
		}

		await ProcessMovieCandidatesAsync(movie.Id, candidates, cancellationToken);
	}

	/// <summary>
	///     Decides and grabs the best approved release per media version, for candidates already matched to the given
	///     movie. Used directly by RSS sync, which matches releases across the whole library up front.
	/// </summary>
	public async Task ProcessMovieCandidatesAsync(int movieId, IReadOnlyList<ReleaseCandidate> candidates, CancellationToken cancellationToken = default)
	{
		if (candidates.Count == 0)
		{
			return;
		}

		var versions = await db.MediaVersions.Where(version => version.MovieId == movieId).ToListAsync(cancellationToken);

		foreach (var version in versions)
		{
			var context = await contextFactory.BuildAsync(version, cancellationToken: cancellationToken);
			var decisions = decisionMaker.DecideAll(candidates, context);
			await GrabBestAsync(decisions, version, null, movieId, cancellationToken);
		}
	}


	private async Task<bool> GrabBestAsync(
		IReadOnlyList<DownloadDecision> decisions,
		MediaVersion version,
		int? seriesId,
		int? movieId,
		CancellationToken cancellationToken)
	{
		var best = decisions.FirstOrDefault(decision => decision.Approved)
			?? decisions.FirstOrDefault(decision => decision.Rejections.Count > 0
				&& decision.Rejections.All(rejection => rejection.Type == RejectionType.TEMPORARY));

		if (best is null)
		{
			return false;
		}

		try
		{
			var outcome = await grabService.GrabAsync(best, version.Id, seriesId, best.Candidate.EpisodeIds, movieId, cancellationToken);
			return outcome is GrabbedOutcome;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Automatic grab of {Title} failed", best.Candidate.Parsed.FullTitle);
			return false;
		}
	}

	private BaseRelease? TryParse(ReleaseInfo info)
	{
		try
		{
			return info.Protocol == Protocol.USENET ? usenetParser.Parse(info.Title) : torrentParser.Parse(info.Title);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
