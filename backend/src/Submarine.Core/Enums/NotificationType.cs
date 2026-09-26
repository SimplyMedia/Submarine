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
	APPRISE,

	/// <summary>Join push notification.</summary>
	JOIN,

	/// <summary>Mailgun email API.</summary>
	MAILGUN,

	/// <summary>Notifiarr webhook relay.</summary>
	NOTIFIARR,

	/// <summary>Prowl push notification.</summary>
	PROWL,

	/// <summary>Pushcut push notification.</summary>
	PUSHCUT,

	/// <summary>Pushsafer push notification.</summary>
	PUSHSAFER,

	/// <summary>SendGrid email API.</summary>
	SENDGRID,

	/// <summary>Signal messenger via signal-cli REST API.</summary>
	SIGNAL,

	/// <summary>Simplepush push notification.</summary>
	SIMPLEPUSH,

	/// <summary>Synology DiskStation media indexer.</summary>
	SYNOLOGY_INDEXER,

	/// <summary>Twitter/X status update or direct message.</summary>
	TWITTER,

	/// <summary>Trakt collection sync, authenticated via OAuth device code flow.</summary>
	TRAKT
}
