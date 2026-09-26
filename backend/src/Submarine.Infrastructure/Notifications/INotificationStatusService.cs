namespace Submarine.Infrastructure.Notifications;

/// <summary>
///     Tracks notification delivery failures and applies an escalating backoff before a notification is
///     retried again.
/// </summary>
public interface INotificationStatusService
{
	/// <summary>Whether the notification is not currently disabled by backoff.</summary>
	Task<bool> IsAvailableAsync(int notificationId, CancellationToken cancellationToken = default);

	/// <summary>Records a successful delivery, clearing any backoff.</summary>
	Task RecordSuccessAsync(int notificationId, CancellationToken cancellationToken = default);

	/// <summary>Records a failed delivery, escalating the backoff window.</summary>
	Task RecordFailureAsync(int notificationId, CancellationToken cancellationToken = default);
}
