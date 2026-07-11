namespace Submarine.Core.Download.Clients;

/// <summary>
///     Connection settings for an <see cref="Aria2Client" />
/// </summary>
public record Aria2Settings
{
	/// <summary>
	///     Hostname or ip of the aria2 rpc interface
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	///     Port of the aria2 rpc interface
	/// </summary>
	public int Port { get; init; } = 6800;

	/// <summary>
	///     Whether the rpc interface is served over https
	/// </summary>
	public bool UseSsl { get; init; }

	/// <summary>
	///     RPC secret token
	/// </summary>
	public string Secret { get; init; } = string.Empty;

	/// <summary>
	///     Absolute url of the aria2 JSON-RPC endpoint
	/// </summary>
	public string Endpoint => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}/jsonrpc";
}
