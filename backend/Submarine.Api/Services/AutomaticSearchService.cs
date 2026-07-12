using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Models.Response;
using Submarine.Api.Repository;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Library;
using Submarine.Core.Release;

namespace Submarine.Api.Services;

/// <summary>
///     Outcome of an automatic search-and-grab run
/// </summary>
/// <param name="VersionsAttempted">Number of monitored versions considered</param>
/// <param name="Grabs">Number of releases grabbed</param>
/// <param name="Skipped">Number of versions skipped because nothing was approved</param>
public sealed record AutomaticSearchResult(int VersionsAttempted, int Grabs, int Skipped);

/// <summary>
///     Runs the search decision pipeline and auto-grabs the best approved release per monitored version
/// </summary>
public class AutomaticSearchService
{
	private readonly SearchService _searchService;
	private readonly GrabService _grabService;
	private readonly BlocklistService _blocklistService;
	private readonly ISeriesRepository _seriesRepository;
	private readonly ILogger<AutomaticSearchService> _logger;

	public AutomaticSearchService(SearchService searchService, GrabService grabService,
		BlocklistService blocklistService, ISeriesRepository seriesRepository,
		ILogger<AutomaticSearchService> logger)
	{
		_searchService = searchService;
		_grabService = grabService;
		_blocklistService = blocklistService;
		_seriesRepository = seriesRepository;
		_logger = logger;
	}

	public async Task<AutomaticSearchResult> SearchAndGrabEpisodeAsync(int episodeId,
		CancellationToken cancellationToken = default)
	{
		var episode = await _seriesRepository.FindEpisodeAsync(episodeId);

		if (episode == null)
			throw new NotFoundException();

		var decisions = await _searchService.SearchEpisodeByIdAsync(episodeId, cancellationToken);

		return await GrabPerVersionAsync(decisions, episode.SeriesId, movieId: null,
			_ => new[] { episodeId }, cancellationToken);
	}

	public async Task<AutomaticSearchResult> SearchAndGrabSeasonAsync(int seriesId, int season,
		CancellationToken cancellationToken = default)
	{
		var decisions = await _searchService.SearchSeasonAsync(seriesId, season, cancellationToken);

		var seasonEpisodes = await _seriesRepository.QueryEpisodes(seriesId)
			.Where(e => e.SeasonNumber == season)
			.ToListAsync(cancellationToken);

		return await GrabPerVersionAsync(decisions, seriesId, movieId: null,
			release => EpisodesForRelease(release, seasonEpisodes), cancellationToken);
	}

	public async Task<AutomaticSearchResult> SearchAndGrabMovieAsync(int movieId,
		CancellationToken cancellationToken = default)
	{
		var decisions = await _searchService.SearchMovieAsync(movieId, cancellationToken);

		return await GrabPerVersionAsync(decisions, seriesId: null, movieId,
			_ => Array.Empty<int>(), cancellationToken);
	}

	private async Task<AutomaticSearchResult> GrabPerVersionAsync(IReadOnlyList<VersionedDownloadDecision> decisions,
		int? seriesId, int? movieId, Func<BaseRelease, IReadOnlyList<int>> resolveEpisodeIds,
		CancellationToken cancellationToken)
	{
		var versionsAttempted = 0;
		var grabs = 0;
		var skipped = 0;

		foreach (var group in decisions.Where(d => d.VersionId != null).GroupBy(d => d.VersionId!.Value))
		{
			versionsAttempted++;

			var best = await BestApprovedAsync(group);

			if (best == null)
			{
				skipped++;
				continue;
			}

			try
			{
				await GrabAsync(best, group.Key, seriesId, movieId,
					resolveEpisodeIds(best.Candidate.Release), cancellationToken);
				grabs++;
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
					throw;

				_logger.LogWarning(ex, "Auto-grab for version {VersionId} failed", group.Key);
			}
		}

		return new AutomaticSearchResult(versionsAttempted, grabs, skipped);
	}

	private async Task<DownloadDecision?> BestApprovedAsync(IEnumerable<VersionedDownloadDecision> group)
	{
		foreach (var versioned in group
			         .Where(d => d.Decision.Approved)
			         .OrderByDescending(d => d.Decision.Score))
		{
			var info = versioned.Decision.Candidate.Info;

			if (await _blocklistService.IsBlockedAsync(info.Guid, info.Title))
				continue;

			return versioned.Decision;
		}

		return null;
	}

	private Task GrabAsync(DownloadDecision decision, int versionId, int? seriesId, int? movieId,
		IReadOnlyList<int> episodeIds, CancellationToken cancellationToken)
	{
		var info = decision.Candidate.Info;

		var request = new GrabReleaseRequest(info.Title, info.Guid, info.DownloadUrl, decision.Candidate.IndexerName,
			info.Protocol, info.Size, seriesId, episodeIds.Count > 0 ? episodeIds : null, movieId, versionId);

		return _grabService.GrabAsync(request, cancellationToken);
	}

	private static IReadOnlyList<int> EpisodesForRelease(BaseRelease release, IReadOnlyList<Episode> seasonEpisodes)
	{
		var data = release.SeriesReleaseData;

		if (data == null)
			return Array.Empty<int>();

		if (data.ReleaseType is SeriesReleaseType.FULL_SEASON or SeriesReleaseType.MULTI_SEASON)
			return seasonEpisodes.Select(e => e.Id).ToList();

		return seasonEpisodes.Where(e => data.Episodes.Contains(e.EpisodeNumber)).Select(e => e.Id).ToList();
	}
}
