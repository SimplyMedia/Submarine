namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the NZBGet JSON-RPC api
/// </summary>
public record NzbGetSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the NZBGet server</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the NZBGet server</summary>
	public int Port { get; init; } = 6789;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path NZBGet is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>Username, if the web interface requires authentication</summary>
	public string Username { get; init; } = string.Empty;

	/// <summary>Password, if the web interface requires authentication</summary>
	public string Password { get; init; } = string.Empty;

	/// <summary>Category nzb files are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Priority for recent releases</summary>
	public DownloadClientPriority RecentPriority { get; init; } = DownloadClientPriority.HIGH;

	/// <summary>Priority for older releases</summary>
	public DownloadClientPriority OlderPriority { get; init; } = DownloadClientPriority.NORMAL;

	/// <summary>Whether nzb files are added paused</summary>
	public bool AddPaused { get; init; }
}
