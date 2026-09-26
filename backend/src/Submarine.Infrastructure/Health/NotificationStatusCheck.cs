using Microsoft.EntityFrameworkCore;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Reports notification connections currently disabled by the escalating failure backoff
///     (see <see cref="Notifications.INotificationStatusService" />).
/// </summary>
public sealed class NotificationStatusCheck(SubmarineDbContext db, TimeProvider timeProvider) : IHealthCheck
{
	private const string Source = "Connect";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var issues = new List<HealthIssueSnapshot>();
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var disabledUntil = await db.NotificationStatuses.AsNoTracking()
			.Where(x => x.DisabledUntil > now)
			.Join(db.Notifications, status => status.NotificationId, notification => notification.Id,
				(status, notification) => new { notification.Name, status.DisabledUntil })
			.ToListAsync(cancellationToken);
		foreach (var status in disabledUntil)
		{
			issues.Add(new(HealthIssueType.WARNING, Source,
				$"Notification {status.Name} is disabled until {status.DisabledUntil:R} after repeated failures", null));
		}

		return issues;
	}
}
