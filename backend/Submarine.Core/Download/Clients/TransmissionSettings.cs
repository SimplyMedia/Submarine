namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="TransmissionClient" />
/// </summary>
public record TransmissionSettings
{
	/// <summary>
	///     Hostname or IP address of the Transmission RPC endpoint
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the Transmission RPC endpoint
	/// </summary>
	public int Port { get; init; } = 9091;

	/// <summary>
	///     Whether to connect using https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Username to authenticate with, if RPC authentication is enabled
	/// </summary>
	public string? Username { get; init; }

	/// <summary>
	///     Password to authenticate with, if RPC authentication is enabled
	/// </summary>
	public string? Password { get; init; }

	/// <summary>
	///     Category assigned to items reported by this client. Transmission has no native category
	///     concept, so this is only used to populate <see cref="DownloadClientItem.Category" />
	/// </summary>
	public string? Category { get; init; }
}
