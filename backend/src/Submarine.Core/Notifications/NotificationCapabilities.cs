using Submarine.Core.Enums;

namespace Submarine.Core.Notifications;

/// <summary>
///     Declares which <see cref="NotificationEventType" /> values each <see cref="NotificationType" /> can deliver,
///     matching which On* handlers the corresponding upstream provider implements. Drives the schema endpoint,
///     the connect UI event toggles and the dispatcher's per-event filtering.
/// </summary>
public static class NotificationCapabilities
{
	private static readonly NotificationEventType[] AllExceptRename =
	[
		NotificationEventType.GRAB,
		NotificationEventType.IMPORT,
		NotificationEventType.UPGRADE,
		NotificationEventType.DELETE,
		NotificationEventType.HEALTH,
		NotificationEventType.HEALTH_RESTORED,
		NotificationEventType.MANUAL_INTERACTION,
		NotificationEventType.APPLICATION_UPDATE
	];

	private static readonly NotificationEventType[] All =
	[
		.. AllExceptRename,
		NotificationEventType.RENAME
	];

	private static readonly NotificationEventType[] MediaImportOnly =
	[
		NotificationEventType.IMPORT,
		NotificationEventType.UPGRADE,
		NotificationEventType.RENAME,
		NotificationEventType.DELETE
	];

	private static readonly NotificationEventType[] ImportUpgradeDeleteOnly =
	[
		NotificationEventType.IMPORT,
		NotificationEventType.UPGRADE,
		NotificationEventType.DELETE
	];

	private static readonly NotificationEventType[] MediaServerRich =
	[
		NotificationEventType.GRAB,
		NotificationEventType.IMPORT,
		NotificationEventType.UPGRADE,
		NotificationEventType.RENAME,
		NotificationEventType.DELETE,
		NotificationEventType.HEALTH,
		NotificationEventType.HEALTH_RESTORED,
		NotificationEventType.APPLICATION_UPDATE
	];

	/// <summary>
	///     The events the notification type can deliver. The dispatcher and the connect UI both use this to
	///     ignore or hide events a provider cannot express, regardless of the per-notification toggles.
	/// </summary>
	public static IReadOnlyList<NotificationEventType> SupportedEvents(NotificationType type)
		=> type switch
		{
			NotificationType.DISCORD => All,
			NotificationType.TELEGRAM => AllExceptRename,
			NotificationType.WEBHOOK => All,
			NotificationType.SLACK => All,
			NotificationType.PUSHOVER => AllExceptRename,
			NotificationType.PUSHBULLET => AllExceptRename,
			NotificationType.GOTIFY => AllExceptRename,
			NotificationType.KODI => All,
			NotificationType.CUSTOM_SCRIPT => All,
			NotificationType.PLEX => MediaImportOnly,
			NotificationType.EMBY => MediaServerRich,
			NotificationType.JELLYFIN => MediaServerRich,
			NotificationType.EMAIL => AllExceptRename,
			NotificationType.NTFY => AllExceptRename,
			NotificationType.APPRISE => AllExceptRename,
			NotificationType.JOIN => AllExceptRename,
			NotificationType.MAILGUN => AllExceptRename,
			NotificationType.NOTIFIARR => All,
			NotificationType.PROWL => AllExceptRename,
			NotificationType.PUSHCUT => AllExceptRename,
			NotificationType.PUSHSAFER => AllExceptRename,
			NotificationType.SENDGRID => AllExceptRename,
			NotificationType.SIGNAL => AllExceptRename,
			NotificationType.SIMPLEPUSH => AllExceptRename,
			NotificationType.SYNOLOGY_INDEXER => MediaImportOnly,
			NotificationType.TWITTER => AllExceptRename,
			NotificationType.TRAKT => ImportUpgradeDeleteOnly,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type")
		};

	/// <summary>Whether the notification type can deliver the given event.</summary>
	public static bool Supports(NotificationType type, NotificationEventType eventType)
		=> SupportedEvents(type).Contains(eventType);
}
