namespace Submarine.Core.Download;

/// <summary>
///     Connection settings for the aria2 JSON-RPC API
/// </summary>
public record Aria2Settings : DownloadClientSettings, IDownloadClientEndpoint
{
	/// <summary>Hostname or ip address of the aria2 rpc interface</summary>
	public string Host { get; init; } = string.Empty;

	/// <summary>Port of the aria2 rpc interface</summary>
	public int Port { get; init; } = 6800;

	/// <summary>Whether to connect using https</summary>
	public bool UseSsl { get; init; }

	/// <summary>Path of the JSON-RPC endpoint</summary>
	public string RpcPath { get; init; } = "/rpc";

	/// <summary>RPC secret token, if the interface requires one</summary>
	public string? SecretToken { get; init; }

	/// <summary>Directory downloads are placed into, if set</summary>
	public string? Directory { get; init; }

	/// <summary>Absolute url of the JSON-RPC endpoint</summary>
	public string RpcUrl => $"{(UseSsl ? "https" : "http")}://{Host}:{Port}{RpcPath}";
}
