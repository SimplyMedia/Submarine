using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Default <see cref="IImportListStatusService" />. Escalates through 15m, 30m, 1h, 3h, 6h, 12h
///     and 24h and stays at 24h afterwards; a single success resets the escalation.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="timeProvider">The time source.</param>
/// <param name="eventBus">Publishes status change notifications for the events hub.</param>
public sealed class ImportListStatusService(SubmarineDbContext db, TimeProvider timeProvider, IEventBus eventBus) : IImportListStatusService
{
	private static readonly TimeSpan[] BackoffLevels =
	[
		TimeSpan.FromMinutes(15),
		TimeSpan.FromMinutes(30),
		TimeSpan.FromHours(1),
		TimeSpan.FromHours(3),
		TimeSpan.FromHours(6),
		TimeSpan.FromHours(12),
		TimeSpan.FromHours(24)
	];

	/// <inheritdoc />
	public async Task<bool> IsAvailableAsync(int importListId, CancellationToken cancellationToken = default)
	{
		var status = await db.ImportListStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.ImportListId == importListId, cancellationToken);
		return status?.DisabledUntil is not { } disabledUntil || disabledUntil <= timeProvider.GetUtcNow().UtcDateTime;
	}

	/// <inheritdoc />
	public async Task<DateTime?> GetLastSyncAsync(int importListId, CancellationToken cancellationToken = default)
	{
		var status = await db.ImportListStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.ImportListId == importListId, cancellationToken);
		return status?.LastSyncAt;
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync(int importListId, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var status = await db.ImportListStatuses.FirstOrDefaultAsync(x => x.ImportListId == importListId, cancellationToken);
		if (status is null)
		{
			status = new ImportListStatus { ImportListId = importListId };
			db.ImportListStatuses.Add(status);
		}

		var hadBackoff = status.EscalationLevel != 0 || status.DisabledUntil is not null;
		status.LastSyncAt = now;
		status.DisabledUntil = null;
		status.InitialFailure = null;
		status.MostRecentFailure = null;
		status.EscalationLevel = 0;

		await db.SaveChangesAsync(cancellationToken);
		if (hadBackoff)
		{
			await eventBus.PublishAsync(new ImportListStatusChangedEvent(importListId), cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(int importListId, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var status = await db.ImportListStatuses.FirstOrDefaultAsync(x => x.ImportListId == importListId, cancellationToken);

		if (status is null)
		{
			status = new ImportListStatus { ImportListId = importListId };
			db.ImportListStatuses.Add(status);
		}

		status.InitialFailure ??= now;
		status.MostRecentFailure = now;

		var level = Math.Min(status.EscalationLevel, BackoffLevels.Length - 1);
		status.DisabledUntil = now + BackoffLevels[level];
		status.EscalationLevel = Math.Min(status.EscalationLevel + 1, BackoffLevels.Length);

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new ImportListStatusChangedEvent(importListId), cancellationToken);
	}
}
