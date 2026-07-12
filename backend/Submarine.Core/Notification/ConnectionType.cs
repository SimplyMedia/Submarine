namespace Submarine.Core.Notification;

/// <summary>
///     The media server a <see cref="Connection" /> talks to
/// </summary>
public enum ConnectionType
{
	/// <summary>Plex Media Server</summary>
	PLEX,

	/// <summary>Emby</summary>
	EMBY,

	/// <summary>Jellyfin</summary>
	JELLYFIN
}
