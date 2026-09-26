using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Submarine.Core.Common;

namespace Submarine.Infrastructure.Persistence.Converters;

/// <summary>
///     Persists any serializable value as a JSON TEXT column. Nulls never reach the converter.
///     Use a nullable type argument for nullable properties, for example JsonValueConverter&lt;QualityModel?&gt;.
/// </summary>
public sealed class JsonValueConverter<TValue> : ValueConverter<TValue, string>
	where TValue : class?
{
	/// <summary>
	///     Shared instance, converters are stateless.
	/// </summary>
	public static readonly JsonValueConverter<TValue> Instance = new();

	/// <summary>
	///     Create the converter.
	/// </summary>
	public JsonValueConverter()
		: base(
			value => JsonSerializer.Serialize(value, SubmarineJson.Default),
			value => JsonSerializer.Deserialize<TValue>(value, SubmarineJson.Default)!)
	{
	}
}
