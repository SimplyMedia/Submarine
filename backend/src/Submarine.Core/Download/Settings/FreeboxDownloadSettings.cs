namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the Freebox Download (native Freebox torrent client) api
/// </summary>
public record FreeboxDownloadSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the Freebox</summary>
	public string Host { get; init; } = "mafreebox.freebox.fr";

	/// <summary>Port of the Freebox api</summary>
	public int Port { get; init; } = 443;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; } = true;

	/// <summary>Base path of the Freebox api, including its version</summary>
	public string ApiUrl { get; init; } = "/api/v1/";

	/// <summary>Application id registered with the Freebox</summary>
	public string AppId { get; init; } = string.Empty;

	/// <summary>Application token granted by the Freebox</summary>
	public string AppToken { get; init; } = string.Empty;

	/// <summary>Destination directory downloads are placed into; mutually exclusive with <see cref="Category" /></summary>
	public string? DestinationDirectory { get; init; }

	/// <summary>Subfolder of the default download directory downloads are placed into</summary>
	public string? Category { get; init; }

	/// <summary>Queue position for recent releases</summary>
	public FreeboxDownloadPriority RecentPriority { get; init; } = FreeboxDownloadPriority.LAST;

	/// <summary>Queue position for older releases</summary>
	public FreeboxDownloadPriority OlderPriority { get; init; } = FreeboxDownloadPriority.LAST;

	/// <summary>Whether downloads are added paused</summary>
	public bool AddPaused { get; init; }

	string? IDownloadClientEndpoint.UrlBase => null;
}

/// <summary>
///     Queue position preference supported by the Freebox Download api
/// </summary>
public enum FreeboxDownloadPriority
{
	/// <summary>Add to the end of the queue</summary>
	LAST,

	/// <summary>Add to the front of the queue</summary>
	FIRST
}
