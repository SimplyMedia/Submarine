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
	WEBHOOK
}
