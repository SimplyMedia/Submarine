using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.IndexerManagement;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Runs a search or RSS fetch against every indexer enabled for the requested mode in parallel, recording per
///     indexer history and backoff status.
/// </summary>
/// <param name="indexerProvider">Builds live indexer clients.</param>
/// <param name="statusService">Tracks indexer failures.</param>
/// <param name="historyRecorder">Records request history.</param>
/// <param name="logger">The logger.</param>
public sealed class ReleaseSearchService(
	IIndexerProvider indexerProvider,
	IIndexerStatusService statusService,
	IndexerHistoryRecorder historyRecorder,
	ILogger<ReleaseSearchService> logger)
{
	private static readonly TimeSpan PerIndexerTimeout = TimeSpan.FromSeconds(60);

	/// <summary>
	///     Searches every indexer enabled for the mode, aggregating results. Never throws for individual indexer
	///     failures; they are recorded and skipped.
	/// </summary>
	/// <param name="request">The search to run.</param>
	/// <param name="mode">Which indexers to include, by search mode.</param>
	/// <param name="source">Label recorded on the resulting history entries.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="indexerIds">When given, restricts the search to this subset of the enabled indexers.</param>
	public async Task<IReadOnlyList<ReleaseInfo>> SearchAsync(
		SearchRequest request,
		IndexerSearchMode mode,
		string source,
		CancellationToken cancellationToken = default,
		IReadOnlyList<int>? indexerIds = null)
	{
		var indexers = await indexerProvider.GetEnabledAsync(mode, cancellationToken);
		if (indexerIds is not null)
		{
			indexers = [.. indexers.Where(configured => indexerIds.Contains(configured.Entity.Id))];
		}

		try
		{
			var outcomes = await Task.WhenAll(indexers.Select(configured => FetchAsync(configured, request, cancellationToken)));
			var results = new List<ReleaseInfo>();

			foreach (var outcome in outcomes)
			{
				if (outcome.Success)
				{
					await statusService.RecordSuccessAsync(outcome.Configured.Entity.Id, cancellationToken);
				}
				else
				{
					await statusService.RecordFailureAsync(outcome.Configured.Entity.Id, cancellationToken);
				}

				await historyRecorder.RecordAsync(
					outcome.Configured.Entity.Id,
					outcome.Success ? (request.IsRss ? IndexerHistoryEventType.RSS : IndexerHistoryEventType.QUERY) : IndexerHistoryEventType.FAILED,
					outcome.Success,
					request.Query,
					string.Join(",", request.Categories),
					source,
					outcome.ElapsedMs,
					cancellationToken);

				results.AddRange(outcome.Releases);
			}

			return results;
		}
		finally
		{
			foreach (var configured in indexers)
			{
				await configured.Client.DisposeAsync();
			}
		}
	}

	private readonly record struct FetchOutcome(
		ConfiguredIndexer Configured,
		IReadOnlyList<ReleaseInfo> Releases,
		bool Success,
		int ElapsedMs);

	private async Task<FetchOutcome> FetchAsync(
		ConfiguredIndexer configured,
		SearchRequest request,
		CancellationToken cancellationToken)
	{
		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(PerIndexerTimeout);
		var stopwatch = Stopwatch.StartNew();

		try
		{
			var releases = request.IsRss
				? await configured.Client.FetchRssAsync(timeoutSource.Token)
				: await configured.Client.FetchAsync(request, timeoutSource.Token);

			stopwatch.Stop();
			return new FetchOutcome(configured, releases, true, (int)stopwatch.ElapsedMilliseconds);
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			stopwatch.Stop();
			logger.LogWarning(exception, "Search of {Indexer} failed", configured.Entity.Name);
			return new FetchOutcome(configured, [], false, (int)stopwatch.ElapsedMilliseconds);
		}
	}
}
