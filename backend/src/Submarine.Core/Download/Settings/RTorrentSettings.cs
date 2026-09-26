namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the rTorrent XML-RPC API
/// </summary>
public record RTorrentSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the rTorrent instance</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the rTorrent scgi or rpc endpoint</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Path of the XML-RPC endpoint</summary>
	public string UrlBase { get; init; } = "RPC2";

	/// <summary>Username, if the endpoint requires http basic auth</summary>
	public string? Username { get; init; }

	/// <summary>Password, if the endpoint requires http basic auth</summary>
	public string? Password { get; init; }

	/// <summary>Custom1 label torrents are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Custom1 label torrents are moved to after import, if different from <see cref="Category" /></summary>
	public string? PostImportCategory { get; init; }

	/// <summary>Directory torrents are downloaded into, if set</summary>
	public string? Directory { get; init; }

	/// <summary>Whether torrents are added without starting them</summary>
	public bool AddStopped { get; init; }

	/// <summary>Transfer priority for recent releases</summary>
	public RTorrentPriority RecentPriority { get; init; } = RTorrentPriority.NORMAL;

	/// <summary>Transfer priority for older releases</summary>
	public RTorrentPriority OlderPriority { get; init; } = RTorrentPriority.NORMAL;
}

/// <summary>
///     Transfer priority supported by rTorrent
/// </summary>
public enum RTorrentPriority
{
	/// <summary>Very low priority</summary>
	VERY_LOW,

	/// <summary>Low priority</summary>
	LOW,

	/// <summary>Normal priority</summary>
	NORMAL,

	/// <summary>High priority</summary>
	HIGH
}
