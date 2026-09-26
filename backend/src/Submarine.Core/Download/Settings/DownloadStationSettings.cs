namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for Synology Download Station
/// </summary>
public record DownloadStationSettings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the DiskStation</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of Download Station</summary>
	public int Port { get; init; } = 5000;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Account of a user with Download Station access</summary>
	public string Username { get; init; } = string.Empty;

	/// <summary>Password of the account</summary>
	public string Password { get; init; } = string.Empty;

	/// <summary>Subfolder of the default destination tasks are placed in, if set; mutually exclusive with <see cref="Directory" /></summary>
	public string? Category { get; init; }

	/// <summary>Destination folder tasks are created in, if set</summary>
	public string? Directory { get; init; }
}
