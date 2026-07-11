using Submarine.Core.DecisionEngine.CustomFormats;

namespace Submarine.Api.Models.Request;

public record CreateCustomFormatRequest
{
	public string Name { get; set; }

	public List<CustomFormatCondition> Conditions { get; set; } = new();
}
