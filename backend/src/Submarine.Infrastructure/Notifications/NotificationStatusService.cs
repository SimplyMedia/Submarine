using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Default <see cref="INotificationStatusService" />. A single isolated failure does not disable the
///     notification: only once a second failure happens after the initial one does the backoff kick in,
///     escalating through 1m, 5m, 15m, 30m, 1h, 3h, 6h, 12h, 24h and staying at 24h afterwards. A single
///     success resets the escalation.
/// </summary>
/// <param name="db">The database context.</param>
/// <param name="timeProvider">The time source.</param>
/// <param name="eventBus">Publishes status change notifications for the events hub.</param>
public sealed class NotificationStatusService(SubmarineDbContext db, TimeProvider timeProvider, IEventBus eventBus) : INotificationStatusService
{
	private static readonly TimeSpan InitialFailureGracePeriod = TimeSpan.FromMinutes(5);

	private static readonly TimeSpan[] BackoffLevels =
	[
		TimeSpan.FromMinutes(1),
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
	public async Task<bool> IsAvailableAsync(int notificationId, CancellationToken cancellationToken = default)
	{
		var status = await db.NotificationStatuses.AsNoTracking().FirstOrDefaultAsync(status => status.NotificationId == notificationId, cancellationToken);
		return status?.DisabledUntil is not { } disabledUntil || disabledUntil <= timeProvider.GetUtcNow().UtcDateTime;
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync(int notificationId, CancellationToken cancellationToken = default)
	{
		var status = await db.NotificationStatuses.FirstOrDefaultAsync(status => status.NotificationId == notificationId, cancellationToken);
		if (status is null || (status.EscalationLevel == 0 && status.DisabledUntil is null && status.InitialFailure is null))
		{
			return;
		}

		status.DisabledUntil = null;
		status.InitialFailure = null;
		status.MostRecentFailure = null;
		status.EscalationLevel = 0;

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new NotificationStatusChangedEvent(notificationId), cancellationToken);
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(int notificationId, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var status = await db.NotificationStatuses.FirstOrDefaultAsync(status => status.NotificationId == notificationId, cancellationToken);

		if (status is null)
		{
			status = new NotificationStatus { NotificationId = notificationId };
			db.NotificationStatuses.Add(status);
		}

		status.MostRecentFailure = now;

		if (status.InitialFailure is null)
		{
			// First failure in a new episode: start tracking but do not disable yet, an isolated
			// failure should not silence the notification.
			status.InitialFailure = now;
		}
		else if (now >= status.InitialFailure.Value + InitialFailureGracePeriod)
		{
			var level = Math.Min(status.EscalationLevel, BackoffLevels.Length - 1);
			status.DisabledUntil = now + BackoffLevels[level];
			status.EscalationLevel = Math.Min(status.EscalationLevel + 1, BackoffLevels.Length);
		}

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new NotificationStatusChangedEvent(notificationId), cancellationToken);
	}
}
