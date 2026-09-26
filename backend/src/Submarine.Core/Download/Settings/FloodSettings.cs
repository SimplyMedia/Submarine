namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the Flood web ui rest api
/// </summary>
public record FloodSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the Flood server</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the Flood server</summary>
	public int Port { get; init; } = 3000;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Url base path Flood is served under, if any</summary>
	public string? UrlBase { get; init; }

	/// <summary>Flood username</summary>
	public string Username { get; init; } = string.Empty;

	/// <summary>Flood password</summary>
	public string Password { get; init; } = string.Empty;

	/// <summary>Destination directory added torrents are placed in, if set</summary>
	public string? Destination { get; init; }

	/// <summary>Tags added torrents receive and are owned by</summary>
	public IReadOnlyList<string> Tags { get; init; } = [];

	/// <summary>Whether torrents are added paused</summary>
	public bool AddPaused { get; init; }
}
