namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the SABnzbd api
/// </summary>
public record SabnzbdSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the SABnzbd server</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the SABnzbd server</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path SABnzbd is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>SABnzbd api key</summary>
	public string ApiKey { get; init; } = string.Empty;

	/// <summary>Username, if basic auth is enabled</summary>
	public string? Username { get; init; }

	/// <summary>Password, if basic auth is enabled</summary>
	public string? Password { get; init; }

	/// <summary>Category nzb files are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Priority for recent releases</summary>
	public DownloadClientPriority RecentPriority { get; init; } = DownloadClientPriority.HIGH;

	/// <summary>Priority for older releases</summary>
	public DownloadClientPriority OlderPriority { get; init; } = DownloadClientPriority.NORMAL;
}
