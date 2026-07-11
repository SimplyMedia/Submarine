namespace Submarine.Core.DecisionEngine.CustomFormats;

/// <summary>
///     A single Condition of a Custom Format
/// </summary>
/// <param name="Name">The Name of this Condition</param>
/// <param name="Type">The Type of this Condition</param>
/// <param name="Value">The Value of this Condition, a regex pattern or enum member name depending on Type</param>
/// <param name="Required">If this Condition must be satisfied for the Custom Format to match</param>
/// <param name="Negated">If the result of this Condition is inverted</param>
public record CustomFormatCondition(
	string Name,
	CustomFormatConditionType Type,
	string Value,
	bool Required,
	bool Negated);
