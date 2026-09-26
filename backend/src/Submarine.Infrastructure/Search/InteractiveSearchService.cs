using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Search;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     The decision made for one media version against a candidate release, returned alongside the release itself.
/// </summary>
public sealed record SearchResultDecision(
	int MediaVersionId,
	bool Approved,
	int Score,
	IReadOnlyList<RejectionReason> Rejections,
	int CustomFormatScore,
	IReadOnlyList<int> CustomFormatIds);

/// <summary>
///     One release found by an interactive search, together with the decision made for every applicable media
///     version.
/// </summary>
public sealed record SearchResult(ReleaseCandidate Candidate, IReadOnlyList<SearchResultDecision> Decisions);

/// <summary>
///     Runs interactive (manual) searches: builds the appropriate indexer request for the requested scope, matches
///     releases back to the library and scores them against every applicable media version.
/// </summary>
public sealed class InteractiveSearchService(
	SubmarineDbContext db,
	ReleaseSearchService searchService,
	ReleaseMatcher matcher,
	DecisionContextFactory contextFactory,
	IDownloadDecisionMaker decisionMaker,
	ReleaseResultCache resultCache,
	EpisodeSearchPlanner episodePlanner,
	IParser<TorrentRelease> torrentParser,
	IParser<UsenetRelease> usenetParser)
{
	/// <summary>
	///     Searches for a series, a season, an episode, a movie or free text, scoring results against every media
	///     version of the matched item, or only <paramref name="mediaVersionId" /> when given.
	/// </summary>
	/// <param name="seriesId">The series to search, alone or with <paramref name="seasonNumber" />.</param>
	/// <param name="seasonNumber">The season to search, requires <paramref name="seriesId" />.</param>
	/// <param name="episodeId">The episode to search.</param>
	/// <param name="movieId">The movie to search.</param>
	/// <param name="term">Free text query, used when none of the library scopes above are given.</param>
	/// <param name="mediaVersionId">Restricts scoring to one media version instead of every applicable one.</param>
	/// <param name="categories">
	///     Standard category ids to restrict the search to. Only applied to the free text <paramref name="term" />
	///     search; the library-scoped searches already carry an implicit type.
	/// </param>
	/// <param name="indexerIds">When given, restricts the search to this subset of the enabled indexers.</param>
	/// <param name="type">
	///     Search type for the free text <paramref name="term" /> search: "tvsearch", "movie", "music" or "book";
	///     anything else (including null) searches basic. Ignored for the library-scoped searches.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task<IReadOnlyList<SearchResult>> SearchAsync(
		int? seriesId,
		int? seasonNumber,
		int? episodeId,
		int? movieId,
		string? term,
		int? mediaVersionId,
		IReadOnlyList<int>? categories = null,
		IReadOnlyList<int>? indexerIds = null,
		string? type = null,
		CancellationToken cancellationToken = default)
	{
		Series? series = null;
		Movie? movie = null;
		IReadOnlyList<ReleaseInfo> releases;

		if (episodeId is { } targetEpisodeId)
		{
			var episode = await db.Episodes.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == targetEpisodeId, cancellationToken)
				?? throw new KeyNotFoundException($"Episode {targetEpisodeId} not found");
			series = await db.Series.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == episode.SeriesId, cancellationToken)
				?? throw new KeyNotFoundException($"Series {episode.SeriesId} not found");
			var requests = await episodePlanner.BuildAsync(series, episode, cancellationToken);
			releases = await SearchManyAsync(requests, indexerIds, cancellationToken);
		}
		else if (seriesId is { } targetSeriesId && seasonNumber is { } targetSeason)
		{
			series = await db.Series.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == targetSeriesId, cancellationToken)
				?? throw new KeyNotFoundException($"Series {targetSeriesId} not found");
			var request = SearchRequestBuilder.BuildSeasonQuery(series.Title, targetSeason, series.TvdbId, series.TmdbId, series.ImdbId);
			releases = await searchService.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "interactive", cancellationToken, indexerIds);
		}
		else if (seriesId is { } onlySeriesId)
		{
			series = await db.Series.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == onlySeriesId, cancellationToken)
				?? throw new KeyNotFoundException($"Series {onlySeriesId} not found");
			var request = SearchRequestBuilder.BuildSeriesQuery(series.Title, series.TvdbId, series.TmdbId, series.ImdbId);
			releases = await searchService.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "interactive", cancellationToken, indexerIds);
		}
		else if (movieId is { } targetMovieId)
		{
			movie = await db.Movies.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == targetMovieId, cancellationToken)
				?? throw new KeyNotFoundException($"Movie {targetMovieId} not found");
			var request = SearchRequestBuilder.BuildMovieQuery(movie.Title, movie.Year, movie.ImdbId, movie.TmdbId);
			releases = await searchService.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "interactive", cancellationToken, indexerIds);
		}
		else if (!string.IsNullOrWhiteSpace(term))
		{
			var request = SearchRequestBuilder.BuildTextQuery(term, categories, type);
			releases = await searchService.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "interactive", cancellationToken, indexerIds);
		}
		else
		{
			throw new ValidationException("One of seriesId, episodeId, movieId or term is required");
		}

		var minimumSeeders = await db.Indexers.AsNoTracking()
			.Select(indexer => new { indexer.Id, indexer.MinimumSeeders })
			.ToDictionaryAsync(indexer => indexer.Id, indexer => indexer.MinimumSeeders, cancellationToken);

		var results = new List<SearchResult>();

		foreach (var group in releases.GroupBy(info => (info.IndexerId, info.Guid)))
		{
			var info = group.First();
			var parsed = TryParse(info);
			if (parsed is null)
			{
				continue;
			}

			var match = series is not null
				? await matcher.MatchSeriesAsync(series, parsed, cancellationToken)
				: movie is not null
					? ReleaseMatcher.MatchMovie(movie)
					: await matcher.MatchLibraryAsync(parsed, cancellationToken);

			var seeders = info.IndexerId is { } indexerId ? minimumSeeders.GetValueOrDefault(indexerId) : null;
			var candidate = new ReleaseCandidate(parsed, info, seeders, match?.SeriesId, match?.MovieId, match?.EpisodeIds);
			resultCache.Store(candidate);

			var versions = await ResolveVersionsAsync(match, mediaVersionId, cancellationToken);
			var decisions = new List<SearchResultDecision>();

			foreach (var version in versions)
			{
				var context = await contextFactory.BuildAsync(version, match?.EpisodeIds, cancellationToken);
				var decision = decisionMaker.Decide(candidate, context);
				decisions.Add(new SearchResultDecision(
					version.Id,
					decision.Approved,
					decision.Score,
					decision.Rejections,
					decision.CustomFormatScore,
					[.. decision.CustomFormats.Select(format => format.Id)]));
			}

			results.Add(new SearchResult(candidate, decisions));
		}

		return [.. results.OrderByDescending(BestScore)];
	}

	private async Task<IReadOnlyList<ReleaseInfo>> SearchManyAsync(
		IReadOnlyList<SearchRequest> requests, IReadOnlyList<int>? indexerIds, CancellationToken cancellationToken)
	{
		var batches = await Task.WhenAll(requests.Select(request =>
			searchService.SearchAsync(request, IndexerSearchMode.INTERACTIVE, "interactive", cancellationToken, indexerIds)));
		return [.. batches.SelectMany(batch => batch).GroupBy(info => (info.IndexerId, info.Guid)).Select(group => group.First())];
	}

	private static int BestScore(SearchResult result)
		=> result.Decisions.Count == 0 ? -1 : result.Decisions.Max(decision => decision.Approved ? decision.Score : -1);

	private async Task<IReadOnlyList<MediaVersion>> ResolveVersionsAsync(ReleaseMatch? match, int? mediaVersionId, CancellationToken cancellationToken)
	{
		if (mediaVersionId is { } id)
		{
			var version = await db.MediaVersions.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
			return version is null ? [] : [version];
		}

		if (match?.SeriesId is { } seriesId)
		{
			return await db.MediaVersions.AsNoTracking().Where(version => version.SeriesId == seriesId).ToListAsync(cancellationToken);
		}

		if (match?.MovieId is { } movieId)
		{
			return await db.MediaVersions.AsNoTracking().Where(version => version.MovieId == movieId).ToListAsync(cancellationToken);
		}

		return [];
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
