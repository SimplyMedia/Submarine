using Submarine.Core.DecisionEngine.CustomFormats;

namespace Submarine.Api.Models.Request;

public record UpdateCustomFormatRequest
{
	public string Name { get; set; }

	public List<CustomFormatCondition> Conditions { get; set; } = new();
}
