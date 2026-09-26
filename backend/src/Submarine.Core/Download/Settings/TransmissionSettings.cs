namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the Transmission RPC API
/// </summary>
public record TransmissionSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the Transmission daemon</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the Transmission rpc interface</summary>
	public int Port { get; init; } = 9091;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the rpc interface is served under</summary>
	public string UrlBase { get; init; } = "/transmission/";

	/// <summary>Username, if the rpc interface requires authentication</summary>
	public string? Username { get; init; }

	/// <summary>Password, if the rpc interface requires authentication</summary>
	public string? Password { get; init; }

	/// <summary>Label (Transmission 4+) added torrents receive and are owned by</summary>
	public string? Category { get; init; }

	/// <summary>Directory torrents are downloaded into, if set</summary>
	public string? Directory { get; init; }

	/// <summary>Whether torrents are added paused</summary>
	public bool AddPaused { get; init; }
}
