using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Submarine.Core.Common;

namespace Submarine.Infrastructure.Persistence.Converters;

/// <summary>
///     Compares JSON-mapped properties by their serialized form so in-place list edits are detected as changes.
/// </summary>
public sealed class JsonValueComparer<TValue> : ValueComparer<TValue>
	where TValue : class?
{
	public JsonValueComparer()
		: base(
			(left, right) => Serialize(left) == Serialize(right),
			value => Serialize(value).GetHashCode(),
			value => value == null ? value : JsonSerializer.Deserialize<TValue>(Serialize(value), SubmarineJson.Default)!)
	{
	}

	private static string Serialize(TValue? value) => JsonSerializer.Serialize(value, SubmarineJson.Default);
}
