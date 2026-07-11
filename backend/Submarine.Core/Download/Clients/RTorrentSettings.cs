namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for a <see cref="RTorrentClient" />
/// </summary>
public record RTorrentSettings
{
	/// <summary>
	///     Hostname or ip of the rTorrent XML-RPC endpoint
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the rTorrent XML-RPC endpoint
	/// </summary>
	public int Port { get; init; } = 80;

	/// <summary>
	///     Whether the endpoint is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     Path of the XML-RPC endpoint
	/// </summary>
	public string UrlBase { get; init; } = "/RPC2";

	/// <summary>
	///     Label applied to added downloads via <c>d.custom1</c>, if set
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	///     Absolute url of the XML-RPC endpoint
	/// </summary>
	public string Endpoint => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}{UrlBase}";
}
