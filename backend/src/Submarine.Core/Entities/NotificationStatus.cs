namespace Submarine.Core.Entities;

/// <summary>
///     Runtime failure state of a notification connection, one row per notification.
/// </summary>
public sealed class NotificationStatus : Entity
{
	/// <summary>Id of the notification, unique.</summary>
	public int NotificationId { get; set; }

	/// <summary>The notification this status belongs to.</summary>
	public Notification Notification { get; set; } = null!;

	/// <summary>UTC timestamp until which the notification is disabled by backoff.</summary>
	public DateTime? DisabledUntil { get; set; }

	/// <summary>UTC timestamp of the first failure in the current escalation window.</summary>
	public DateTime? InitialFailure { get; set; }

	/// <summary>UTC timestamp of the most recent failure.</summary>
	public DateTime? MostRecentFailure { get; set; }

	/// <summary>Current backoff escalation level.</summary>
	public int EscalationLevel { get; set; }
}
