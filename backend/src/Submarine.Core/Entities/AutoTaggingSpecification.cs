using System.Text.Json;
using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     One specification of an auto tagging rule. The shape of <see cref="Value" /> depends on <see cref="Type" />.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Type">Specification type.</param>
/// <param name="Negate">Invert the match result.</param>
/// <param name="Required">The series or movie must match for the rule to apply.</param>
/// <param name="Value">Type specific value, for example a genre list, enum member or min/max range.</param>
public sealed record AutoTaggingSpecification(
	string Name,
	AutoTaggingSpecificationType Type,
	bool Negate,
	bool Required,
	JsonElement? Value);
