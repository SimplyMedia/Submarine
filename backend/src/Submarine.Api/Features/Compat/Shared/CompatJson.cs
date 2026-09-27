using System.Text.Json;
using System.Text.Json.Serialization;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>JSON contract for compatibility responses, isolated from native API serialization.</summary>
public static class CompatJson
{
	public static JsonSerializerOptions Options { get; } = CreateOptions();

	private static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.Never
		};
		options.MakeReadOnly(populateMissingResolver: true);
		return options;
	}
}
