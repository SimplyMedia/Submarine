namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="QBittorrentClient" />
/// </summary>
public record QBittorrentSettings
{
	/// <summary>
	///     Hostname or IP address of the qBittorrent WebUI
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the qBittorrent WebUI
	/// </summary>
	public int Port { get; init; } = 8080;

	/// <summary>
	///     Whether to connect using https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Username to authenticate with, if the WebUI requires authentication
	/// </summary>
	public string? Username { get; init; }

	/// <summary>
	///     Password to authenticate with, if the WebUI requires authentication
	/// </summary>
	public string? Password { get; init; }

	/// <summary>
	///     Category torrents are added under and filtered by
	/// </summary>
	public string? Category { get; init; }
}
