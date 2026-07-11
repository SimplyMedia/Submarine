using Submarine.Core.DecisionEngine.Filter;

namespace Submarine.Api.Models.Request;

public record CreateReleaseFilterRequest
{
	public FilterField Field { get; set; }

	public List<string> Values { get; set; } = new();

	public FilterMode Mode { get; set; }

	public int Tier { get; set; }
}
