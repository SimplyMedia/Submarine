using System.Text.Json;
using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     One specification of a custom format. The shape of <see cref="Value" /> depends on <see cref="Type" />.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Specification type.</param>
/// <param name="Negate">Invert the match result.</param>
/// <param name="Required">Release must match for the format to apply.</param>
/// <param name="Value">Type specific value, for example a regex, enum member or min/max range.</param>
public sealed record CustomFormatSpecification(
	string Name,
	CustomFormatSpecificationType Type,
	bool Negate,
	bool Required,
	JsonElement? Value);
