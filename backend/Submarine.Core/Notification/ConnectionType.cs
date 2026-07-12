namespace Submarine.Core.Notification;

/// <summary>
///     The media server or notification target a <see cref="Connection" /> talks to
/// </summary>
public enum ConnectionType
{
	/// <summary>Plex Media Server</summary>
	PLEX,

	/// <summary>Emby</summary>
	EMBY,

	/// <summary>Jellyfin</summary>
	JELLYFIN,

	/// <summary>Discord, via <see cref="DiscordConnection" /></summary>
	DISCORD,

	/// <summary>Telegram, via <see cref="TelegramConnection" /></summary>
	TELEGRAM,

	/// <summary>A generic webhook, via <see cref="WebhookConnection" /></summary>
	WEBHOOK,

	/// <summary>Slack, via <see cref="SlackConnection" /></summary>
	SLACK,

	/// <summary>Pushover, via <see cref="PushoverConnection" /></summary>
	PUSHOVER,

	/// <summary>Pushbullet, via <see cref="PushbulletConnection" /></summary>
	PUSHBULLET,

	/// <summary>Gotify, via <see cref="GotifyConnection" /></summary>
	GOTIFY,

	/// <summary>Kodi media player, via <see cref="KodiConnection" /></summary>
	KODI,

	/// <summary>A locally executed script, via <see cref="CustomScriptConnection" /></summary>
	CUSTOM_SCRIPT
}
