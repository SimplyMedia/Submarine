using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Default <see cref="IIndexerStatusService" />. Escalates through 5m, 15m, 30m, 1h, 3h, 6h, 12h, 24h and stays at
///     24h afterwards; a single success resets the escalation.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="timeProvider">The time source.</param>
/// <param name="eventBus">Publishes status change notifications for the events hub.</param>
public sealed class IndexerStatusService(SubmarineDbContext db, TimeProvider timeProvider, IEventBus eventBus) : IIndexerStatusService
{
	private static readonly TimeSpan[] BackoffLevels =
	[
		TimeSpan.FromMinutes(5),
		TimeSpan.FromMinutes(15),
		TimeSpan.FromMinutes(30),
		TimeSpan.FromHours(1),
		TimeSpan.FromHours(3),
		TimeSpan.FromHours(6),
		TimeSpan.FromHours(12),
		TimeSpan.FromHours(24)
	];

	/// <inheritdoc />
	public async Task<bool> IsAvailableAsync(int indexerId, CancellationToken cancellationToken = default)
	{
		var status = await db.IndexerStatuses.AsNoTracking().FirstOrDefaultAsync(status => status.IndexerId == indexerId, cancellationToken);
		return status?.DisabledUntil is not { } disabledUntil || disabledUntil <= timeProvider.GetUtcNow().UtcDateTime;
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync(int indexerId, CancellationToken cancellationToken = default)
	{
		var status = await db.IndexerStatuses.FirstOrDefaultAsync(status => status.IndexerId == indexerId, cancellationToken);
		if (status is null || (status.EscalationLevel == 0 && status.DisabledUntil is null))
		{
			return;
		}

		status.DisabledUntil = null;
		status.InitialFailure = null;
		status.MostRecentFailure = null;
		status.EscalationLevel = 0;

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new IndexerStatusChangedEvent(indexerId), cancellationToken);
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(int indexerId, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var status = await db.IndexerStatuses.FirstOrDefaultAsync(status => status.IndexerId == indexerId, cancellationToken);

		if (status is null)
		{
			status = new IndexerStatus { IndexerId = indexerId };
			db.IndexerStatuses.Add(status);
		}

		status.InitialFailure ??= now;
		status.MostRecentFailure = now;

		var level = Math.Min(status.EscalationLevel, BackoffLevels.Length - 1);
		status.DisabledUntil = now + BackoffLevels[level];
		status.EscalationLevel = Math.Min(status.EscalationLevel + 1, BackoffLevels.Length);

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new IndexerStatusChangedEvent(indexerId), cancellationToken);
	}
}
