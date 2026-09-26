namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the RQBit REST api
/// </summary>
public record RQbitSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the RQBit daemon</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the RQBit api</summary>
	public int Port { get; init; } = 3030;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path the api is served under, if any</summary>
	public string? UrlBase { get; init; }
}
