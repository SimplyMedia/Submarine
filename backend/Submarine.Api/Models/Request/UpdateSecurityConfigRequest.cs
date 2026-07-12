using Submarine.Core.Config;

namespace Submarine.Api.Models.Request;

public record UpdateSecurityConfigRequest
{
	public AuthenticationMethod Method { get; set; }

	public string? NewApiKey { get; set; }

	public bool Regenerate { get; set; }
}
