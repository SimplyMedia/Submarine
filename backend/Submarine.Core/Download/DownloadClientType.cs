namespace Submarine.Core.Download;

/// <summary>
///     The concrete type of a Download Client
/// </summary>
public enum DownloadClientType
{
	/// <summary>qBittorrent</summary>
	QBITTORRENT,

	/// <summary>Transmission</summary>
	TRANSMISSION,

	/// <summary>Deluge</summary>
	DELUGE,

	/// <summary>rTorrent</summary>
	RTORRENT,

	/// <summary>uTorrent</summary>
	UTORRENT,

	/// <summary>Aria2</summary>
	ARIA2,

	/// <summary>Flood</summary>
	FLOOD,

	/// <summary>Synology Download Station</summary>
	DOWNLOAD_STATION,

	/// <summary>SABnzbd</summary>
	SABNZBD,

	/// <summary>NZBGet</summary>
	NZBGET,

	/// <summary>Torrent Blackhole</summary>
	TORRENT_BLACKHOLE,

	/// <summary>Usenet Blackhole</summary>
	USENET_BLACKHOLE
}
