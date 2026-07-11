namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="SabnzbdClient" />
/// </summary>
public record SabnzbdSettings
{
	/// <summary>
	///     Hostname or IP address of the SABnzbd instance
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the SABnzbd instance
	/// </summary>
	public int Port { get; init; } = 8080;

	/// <summary>
	///     Whether to connect using https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Username to authenticate with, if SABnzbd sits behind a Basic auth proxy
	/// </summary>
	public string? Username { get; init; }

	/// <summary>
	///     Password to authenticate with, if SABnzbd sits behind a Basic auth proxy
	/// </summary>
	public string? Password { get; init; }

	/// <summary>
	///     Category downloads are added under
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     SABnzbd API key
	/// </summary>
	public required string ApiKey { get; init; }
}
