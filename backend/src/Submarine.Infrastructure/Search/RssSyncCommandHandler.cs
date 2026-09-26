using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Commands;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Fetches RSS from every RSS-enabled indexer in parallel, deduping against the last seen release per indexer,
///     matches releases to the library and grabs the best approved release per media version and matched episode
///     set. Runs <see cref="ProcessPendingReleasesCommand" /> at the end so newly unblocked pending releases go out
///     in the same cycle.
/// </summary>
public sealed class RssSyncCommandHandler(
	SubmarineDbContext db,
	IIndexerProvider indexerProvider,
	IIndexerStatusService statusService,
	IndexerHistoryRecorder historyRecorder,
	ReleaseMatcher matcher,
	AutomaticSearchService automaticSearchService,
	ICommandQueue commandQueue,
	IParser<TorrentRelease> torrentParser,
	IParser<UsenetRelease> usenetParser,
	ILogger<RssSyncCommandHandler> logger) : ICommandHandler<RssSyncCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RssSyncCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var indexerConfig = await db.IndexerConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
		if (indexerConfig?.RssSyncIntervalMinutes == 0)
		{
			return;
		}

		var indexers = await indexerProvider.GetEnabledAsync(IndexerSearchMode.RSS, cancellationToken);
		try
		{
			var outcomes = await Task.WhenAll(indexers.Select(configured => FetchAsync(configured, cancellationToken)));
			var seriesCandidates = new Dictionary<int, List<ReleaseCandidate>>();
			var movieCandidates = new Dictionary<int, List<ReleaseCandidate>>();

			foreach (var outcome in outcomes)
			{
				var fresh = await RecordAndDedupeAsync(outcome, cancellationToken);

				foreach (var info in fresh)
				{
					var parsed = TryParse(info);
					if (parsed is null)
					{
						continue;
					}

					var match = await matcher.MatchLibraryAsync(parsed, cancellationToken);
					if (match is null)
					{
						continue;
					}

					var candidate = new ReleaseCandidate(parsed, info, null, match.SeriesId, match.MovieId, match.EpisodeIds);

					if (match.SeriesId is { } seriesId)
					{
						(seriesCandidates.TryGetValue(seriesId, out var list) ? list : seriesCandidates[seriesId] = []).Add(candidate);
					}
					else if (match.MovieId is { } movieId)
					{
						(movieCandidates.TryGetValue(movieId, out var list) ? list : movieCandidates[movieId] = []).Add(candidate);
					}
				}
			}

			foreach (var (seriesId, candidates) in seriesCandidates)
			{
				await automaticSearchService.ProcessSeriesCandidatesAsync(seriesId, candidates, cancellationToken);
			}

			foreach (var (movieId, candidates) in movieCandidates)
			{
				await automaticSearchService.ProcessMovieCandidatesAsync(movieId, candidates, cancellationToken);
			}
		}
		finally
		{
			foreach (var configured in indexers)
			{
				await configured.Client.DisposeAsync();
			}
		}

		await commandQueue.EnqueueAsync(new ProcessPendingReleasesCommand(), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
	}

	private readonly record struct RssFetchOutcome(ConfiguredIndexer Configured, IReadOnlyList<ReleaseInfo> Releases, bool Success, Exception? Exception);

	private async Task<RssFetchOutcome> FetchAsync(ConfiguredIndexer configured, CancellationToken cancellationToken)
	{
		try
		{
			var releases = await configured.Client.FetchRssAsync(cancellationToken);
			return new RssFetchOutcome(configured, releases, true, null);
		}
		catch (Exception exception)
		{
			return new RssFetchOutcome(configured, [], false, exception);
		}
	}

	// records status/history sequentially against the shared scoped db context, then dedupes against the last seen
	// release for the indexer
	private async Task<IReadOnlyList<ReleaseInfo>> RecordAndDedupeAsync(RssFetchOutcome outcome, CancellationToken cancellationToken)
	{
		if (!outcome.Success)
		{
			logger.LogWarning(outcome.Exception, "RSS sync of {Indexer} failed", outcome.Configured.Entity.Name);
			await statusService.RecordFailureAsync(outcome.Configured.Entity.Id, cancellationToken);
			await historyRecorder.RecordAsync(outcome.Configured.Entity.Id, IndexerHistoryEventType.FAILED, false, null, null, "rss-sync", null, cancellationToken);
			return [];
		}

		await statusService.RecordSuccessAsync(outcome.Configured.Entity.Id, cancellationToken);
		await historyRecorder.RecordAsync(outcome.Configured.Entity.Id, IndexerHistoryEventType.RSS, true, null, null, "rss-sync", null, cancellationToken);

		var status = await db.IndexerStatuses.AsNoTracking().FirstOrDefaultAsync(row => row.IndexerId == outcome.Configured.Entity.Id, cancellationToken);
		var known = status?.LastRssSyncReleaseInfo;
		var fresh = known is null ? outcome.Releases : [.. outcome.Releases.TakeWhile(release => release.Guid != known && release.InfoHash != known)];

		var newest = outcome.Releases.FirstOrDefault()?.Guid;
		if (!string.IsNullOrEmpty(newest))
		{
			var row = await db.IndexerStatuses.FirstOrDefaultAsync(candidate => candidate.IndexerId == outcome.Configured.Entity.Id, cancellationToken);
			if (row is null)
			{
				row = new Core.Entities.IndexerStatus { IndexerId = outcome.Configured.Entity.Id };
				db.IndexerStatuses.Add(row);
			}

			row.LastRssSyncReleaseInfo = newest;
			await db.SaveChangesAsync(cancellationToken);
		}

		return fresh;
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
