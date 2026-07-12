using Submarine.Core.Notification;

namespace Submarine.Api.Models.Request;

public record CreateConnectionRequest
{
	public string Name { get; set; }

	public ConnectionType Type { get; set; }

	public bool Enable { get; set; }

	public string Host { get; set; }

	public int Port { get; set; }

	public bool UseSsl { get; set; }

	public string ApiKey { get; set; }

	public bool OnGrab { get; set; }

	public bool OnImport { get; set; }

	public bool OnRename { get; set; }

	public List<string> Tags { get; set; } = new();
}
