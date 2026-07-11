using Submarine.Core.Provider;

namespace Submarine.Api.Models.Request;

public record UpdateProviderRequest
{
	public string? Name { get; set; }

	public ProviderMode? Mode { get; set; }

	public string? Url { get; set; }

	public string? ApiKey { get; set; }

	public short? Priority { get; set; }
}
