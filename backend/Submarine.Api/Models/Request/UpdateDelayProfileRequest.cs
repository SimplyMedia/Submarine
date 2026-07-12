using Submarine.Core.Provider;

namespace Submarine.Api.Models.Request;

public record UpdateDelayProfileRequest
{
	public string Name { get; set; } = null!;

	public Protocol PreferredProtocol { get; set; }

	public int UsenetDelayMinutes { get; set; }

	public int TorrentDelayMinutes { get; set; }

	public bool BypassIfHighestQuality { get; set; }

	public int Order { get; set; }

	public List<string> Tags { get; set; } = new();
}
