namespace Submarine.Core.DecisionEngine.CustomFormats;

/// <summary>
///     A Custom Format, a named set of Conditions evaluated against a Release
/// </summary>
/// <param name="Id">The Id of this Custom Format</param>
/// <param name="Name">The Name of this Custom Format</param>
/// <param name="Conditions">The Conditions of this Custom Format</param>
public record CustomFormat(
	int Id,
	string Name,
	IReadOnlyList<CustomFormatCondition> Conditions);
