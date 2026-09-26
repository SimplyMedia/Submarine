namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the qBittorrent WebUI API v2
/// </summary>
public record QBittorrentSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the qBittorrent WebUI</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the qBittorrent WebUI</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the WebUI is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>
	///     Api key used to bypass WebUI login, e.g. behind a reverse proxy; when set, username/password are
	///     ignored and requests use a Bearer token instead of session cookies
	/// </summary>
	public string? ApiKey { get; init; }

	/// <summary>Username, if the WebUI requires authentication</summary>
	public string? Username { get; init; }

	/// <summary>Password, if the WebUI requires authentication</summary>
	public string? Password { get; init; }

	/// <summary>Category torrents are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Category torrents are moved to after import, if set</summary>
	public string? PostImportCategory { get; init; }

	/// <summary>Queue position for recent releases</summary>
	public DownloadClientItemPriority RecentPriority { get; init; } = DownloadClientItemPriority.FIRST;

	/// <summary>Queue position for older releases</summary>
	public DownloadClientItemPriority OlderPriority { get; init; } = DownloadClientItemPriority.LAST;

	/// <summary>State torrents start in</summary>
	public QBittorrentInitialState InitialState { get; init; } = QBittorrentInitialState.START;

	/// <summary>Whether to download pieces in sequential order</summary>
	public bool SequentialOrder { get; init; }

	/// <summary>Whether to prioritize first and last pieces</summary>
	public bool FirstAndLast { get; init; }

	/// <summary>How the torrent's content is laid out on disk</summary>
	public QBittorrentContentLayout ContentLayout { get; init; } = QBittorrentContentLayout.DEFAULT;
}

/// <summary>
///     Initial state of torrents added to qBittorrent
/// </summary>
public enum QBittorrentInitialState
{
	/// <summary>Start downloading</summary>
	START,

	/// <summary>Force start, ignoring queue limits</summary>
	FORCE_START,

	/// <summary>Add paused</summary>
	PAUSE
}

/// <summary>
///     Content layout for torrents added to qBittorrent
/// </summary>
public enum QBittorrentContentLayout
{
	/// <summary>Use qBittorrent's global default</summary>
	DEFAULT,

	/// <summary>Keep the torrent's original layout, even for single-file torrents with a subfolder</summary>
	ORIGINAL,

	/// <summary>Always create a subfolder</summary>
	SUBFOLDER
}
