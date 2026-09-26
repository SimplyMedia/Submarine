namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the Hadouken JSON-RPC API
/// </summary>
public record HadoukenSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the Hadouken daemon</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the Hadouken web api</summary>
	public int Port { get; init; } = 7070;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the web api is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>Username, required to authenticate</summary>
	public string? Username { get; init; }

	/// <summary>Password, required to authenticate</summary>
	public string? Password { get; init; }

	/// <summary>Label torrents are added under and owned by</summary>
	public string? Category { get; init; }
}
