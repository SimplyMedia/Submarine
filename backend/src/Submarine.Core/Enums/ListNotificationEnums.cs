namespace Submarine.Core.Enums;

/// <summary>
///     Type of an import list.
/// </summary>
public enum ImportListType
{
	/// <summary>TMDB list.</summary>
	TMDB_LIST,

	/// <summary>TMDB popular.</summary>
	TMDB_POPULAR,

	/// <summary>TMDB collection.</summary>
	TMDB_COLLECTION,

	/// <summary>TMDB person.</summary>
	TMDB_PERSON,

	/// <summary>Trakt list.</summary>
	TRAKT_LIST,

	/// <summary>Trakt popular.</summary>
	TRAKT_POPULAR,

	/// <summary>Trakt user.</summary>
	TRAKT_USER,

	/// <summary>AniList season.</summary>
	ANILIST_SEASON,

	/// <summary>Plex watchlist.</summary>
	PLEX,

	/// <summary>Another Sonarr instance.</summary>
	SONARR,

	/// <summary>Another Radarr instance.</summary>
	RADARR,

	/// <summary>StevenLu list.</summary>
	STEVEN_LU,

	/// <summary>Custom list.</summary>
	CUSTOM
}

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
