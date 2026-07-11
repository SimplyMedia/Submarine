namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="DelugeClient" />
/// </summary>
public record DelugeSettings
{
	/// <summary>
	///     Hostname or ip of the Deluge web ui
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the Deluge web ui
	/// </summary>
	public int Port { get; init; } = 8112;

	/// <summary>
	///     Whether the web ui is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Web ui password
	/// </summary>
	public required string Password { get; init; }

	/// <summary>
	///     Label applied to added downloads, if set
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     Absolute url of the Deluge JSON-RPC endpoint
	/// </summary>
	public string Endpoint => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}/json";
}
