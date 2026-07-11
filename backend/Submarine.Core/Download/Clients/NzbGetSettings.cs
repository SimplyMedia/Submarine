namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="NzbGetClient" />
/// </summary>
public record NzbGetSettings
{
	/// <summary>
	///     Hostname or IP address of the NZBGet instance
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the NZBGet instance
	/// </summary>
	public int Port { get; init; } = 6789;

	/// <summary>
	///     Whether to connect using https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Username to authenticate with
	/// </summary>
	public string? Username { get; init; }

	/// <summary>
	///     Password to authenticate with
	/// </summary>
	public string? Password { get; init; }

	/// <summary>
	///     Category downloads are added under
	/// </summary>
	public string? Category { get; init; }
}
