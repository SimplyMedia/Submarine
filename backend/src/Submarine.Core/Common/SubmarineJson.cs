using System.Text.Json;
using System.Text.Json.Serialization;

namespace Submarine.Core.Common;

/// <summary>
///     Shared JSON serializer options used for JSON columns, command bodies and realtime payloads.
///     Property names are camelCase, enums are serialized as their member names and reads are case-insensitive.
/// </summary>
public static class SubmarineJson
{
	/// <summary>
	///     The shared serializer options.
	/// </summary>
	public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};
}
