using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Enforces the per-indexer query and grab limits configured on <see cref="Indexer.QueryLimit" /> and
///     <see cref="Indexer.GrabLimit" />, counted from <see cref="IndexerHistory" /> rows over the indexer's
///     <see cref="Indexer.LimitsUnit" /> window (24 hours for <see cref="IndexerLimitsUnit.DAY" />, 1 hour for
///     <see cref="IndexerLimitsUnit.HOUR" />).
/// </summary>
public interface IIndexerLimitService
{
	/// <summary>Whether the indexer has reached its configured query (search plus RSS) limit for the current window.</summary>
	Task<bool> AtQueryLimitAsync(Indexer indexer, CancellationToken cancellationToken = default);

	/// <summary>Whether the indexer has reached its configured grab limit for the current window.</summary>
	Task<bool> AtGrabLimitAsync(Indexer indexer, CancellationToken cancellationToken = default);

	/// <summary>Seconds until the query limit window frees up a slot, or 0 when not currently limited.</summary>
	Task<int> CalculateRetryAfterQueryLimitAsync(Indexer indexer, CancellationToken cancellationToken = default);

	/// <summary>Seconds until the grab limit window frees up a slot, or 0 when not currently limited.</summary>
	Task<int> CalculateRetryAfterGrabLimitAsync(Indexer indexer, CancellationToken cancellationToken = default);

	/// <summary>The interval, in hours, that <see cref="Indexer.QueryLimit" /> and <see cref="Indexer.GrabLimit" /> apply over.</summary>
	int IntervalHours(Indexer indexer);
}

/// <inheritdoc cref="IIndexerLimitService" />
public sealed class IndexerLimitService(SubmarineDbContext db, TimeProvider timeProvider) : IIndexerLimitService
{
	private static readonly IndexerHistoryEventType[] QueryEventTypes = [IndexerHistoryEventType.QUERY, IndexerHistoryEventType.RSS];
	private static readonly IndexerHistoryEventType[] GrabEventTypes = [IndexerHistoryEventType.GRAB];

	/// <inheritdoc />
	public Task<bool> AtQueryLimitAsync(Indexer indexer, CancellationToken cancellationToken = default)
		=> AtLimitAsync(indexer, indexer.QueryLimit, QueryEventTypes, cancellationToken);

	/// <inheritdoc />
	public Task<bool> AtGrabLimitAsync(Indexer indexer, CancellationToken cancellationToken = default)
		=> AtLimitAsync(indexer, indexer.GrabLimit, GrabEventTypes, cancellationToken);

	/// <inheritdoc />
	public Task<int> CalculateRetryAfterQueryLimitAsync(Indexer indexer, CancellationToken cancellationToken = default)
		=> CalculateRetryAfterAsync(indexer, indexer.QueryLimit, QueryEventTypes, cancellationToken);

	/// <inheritdoc />
	public Task<int> CalculateRetryAfterGrabLimitAsync(Indexer indexer, CancellationToken cancellationToken = default)
		=> CalculateRetryAfterAsync(indexer, indexer.GrabLimit, GrabEventTypes, cancellationToken);

	/// <inheritdoc />
	public int IntervalHours(Indexer indexer)
		=> indexer.LimitsUnit == IndexerLimitsUnit.HOUR ? 1 : 24;

	private async Task<bool> AtLimitAsync(Indexer indexer, int? limit, IndexerHistoryEventType[] eventTypes, CancellationToken cancellationToken)
	{
		if (limit is not { } value)
		{
			return false;
		}

		var since = timeProvider.GetUtcNow().UtcDateTime.AddHours(-IntervalHours(indexer));
		var count = await db.IndexerHistories.AsNoTracking()
			.CountAsync(entry => entry.IndexerId == indexer.Id && entry.Date >= since && eventTypes.Contains(entry.EventType), cancellationToken);

		return count >= value;
	}

	private async Task<int> CalculateRetryAfterAsync(Indexer indexer, int? limit, IndexerHistoryEventType[] eventTypes, CancellationToken cancellationToken)
	{
		if (limit is not { } value || value <= 0)
		{
			return 0;
		}

		var intervalHours = IntervalHours(indexer);
		var since = timeProvider.GetUtcNow().UtcDateTime.AddHours(-intervalHours);

		// The window frees up once the oldest of the most recent `value` matching entries ages out.
		var recent = await db.IndexerHistories.AsNoTracking()
			.Where(entry => entry.IndexerId == indexer.Id && entry.Date >= since && eventTypes.Contains(entry.EventType))
			.OrderByDescending(entry => entry.Date)
			.Take(value)
			.ToListAsync(cancellationToken);

		if (recent.Count < value)
		{
			return 0;
		}

		var oldest = recent[^1];
		var retryAt = oldest.Date.AddHours(intervalHours);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		return retryAt > now ? (int)(retryAt - now).TotalSeconds : 0;
	}
}
