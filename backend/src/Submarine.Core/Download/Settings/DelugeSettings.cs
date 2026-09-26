namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the Deluge web ui JSON-RPC API
/// </summary>
public record DelugeSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the Deluge web ui</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the Deluge web ui</summary>
	public int Port { get; init; } = 8112;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the web ui is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>Web ui password</summary>
	public string Password { get; init; } = string.Empty;

	/// <summary>Label torrents are added under and owned by, requires the label plugin</summary>
	public string? Category { get; init; }

	/// <summary>Whether torrents are added paused</summary>
	public bool AddPaused { get; init; }
}
