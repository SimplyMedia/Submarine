namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="UTorrentClient" />
/// </summary>
public record UTorrentSettings
{
	/// <summary>
	///     Hostname or ip of the uTorrent web ui
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the uTorrent web ui
	/// </summary>
	public int Port { get; init; } = 8080;

	/// <summary>
	///     Whether the web ui is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Web ui username
	/// </summary>
	public string? Username { get; init; }

	/// <summary>
	///     Web ui password
	/// </summary>
	public string? Password { get; init; }

	/// <summary>
	///     Absolute url of the uTorrent web ui gui root
	/// </summary>
	public string BaseUrl => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}/gui";
}
