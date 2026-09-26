namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the uTorrent web ui
/// </summary>
public record UTorrentSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the uTorrent web ui</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the uTorrent web ui</summary>
	public int Port { get; init; } = 8080;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the web ui is served under, defaults to /gui/</summary>
	public string? UrlBase { get; init; }

	/// <summary>Username, if the web ui requires authentication</summary>
	public string? Username { get; init; }

	/// <summary>Password, if the web ui requires authentication</summary>
	public string? Password { get; init; }

	/// <summary>Label torrents are added under and owned by</summary>
	public string? Category { get; init; }

	/// <summary>Whether torrents are added paused</summary>
	public bool AddStopped { get; init; }

	/// <summary>Effective url base; uTorrent serves its web ui under /gui/</summary>
	public string EffectiveUrlBase => UrlBase ?? "/gui/";
}
