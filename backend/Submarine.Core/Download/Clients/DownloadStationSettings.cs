namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="DownloadStationClient" />
/// </summary>
public record DownloadStationSettings
{
	/// <summary>
	///     Hostname or ip of the Synology DiskStation
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the DiskStation web api
	/// </summary>
	public int Port { get; init; } = 5000;

	/// <summary>
	///     Whether the web api is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     DiskStation account name
	/// </summary>
	public required string Username { get; init; }

	/// <summary>
	///     DiskStation account password
	/// </summary>
	public required string Password { get; init; }

	/// <summary>
	///     Absolute url of the DiskStation web api root
	/// </summary>
	public string BaseUrl => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}";
}
