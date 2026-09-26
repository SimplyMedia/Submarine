namespace Submarine.Core.Enums;

/// <summary>
///     Type of a notification.
/// </summary>
public enum NotificationType
{
	/// <summary>Discord webhook.</summary>
	DISCORD,

	/// <summary>Telegram bot.</summary>
	TELEGRAM,

	/// <summary>Generic webhook.</summary>
	WEBHOOK,

	/// <summary>Slack webhook.</summary>
	SLACK,

	/// <summary>Pushover.</summary>
	PUSHOVER,

	/// <summary>Pushbullet.</summary>
	PUSHBULLET,

	/// <summary>Gotify.</summary>
	GOTIFY,

	/// <summary>Kodi JSON-RPC.</summary>
	KODI,

	/// <summary>Custom script.</summary>
	CUSTOM_SCRIPT,

	/// <summary>Plex server.</summary>
	PLEX,

	/// <summary>Emby server.</summary>
	EMBY,

	/// <summary>Jellyfin server.</summary>
	JELLYFIN,

	/// <summary>Email.</summary>
	EMAIL,

	/// <summary>ntfy.</summary>
	NTFY,

	/// <summary>Apprise.</summary>
	APPRISE
}
