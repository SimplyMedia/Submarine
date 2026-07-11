namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="FloodClient" />
/// </summary>
public record FloodSettings
{
	/// <summary>
	///     Hostname or ip of the Flood server
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the Flood server
	/// </summary>
	public int Port { get; init; } = 3000;

	/// <summary>
	///     Whether the server is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Flood username
	/// </summary>
	public required string Username { get; init; }

	/// <summary>
	///     Flood password
	/// </summary>
	public required string Password { get; init; }

	/// <summary>
	///     Destination directory added downloads are placed in, if set
	/// </summary>
	public string? Destination { get; init; }

	/// <summary>
	///     Tag applied to added downloads, if set
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     Absolute url of the Flood server root
	/// </summary>
	public string BaseUrl => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}";
}
