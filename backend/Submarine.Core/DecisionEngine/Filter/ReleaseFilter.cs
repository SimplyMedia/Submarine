namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     A Filter matching a <see cref="FilterField" /> of a Release against a set of values
/// </summary>
/// <param name="Id">The Id of this Filter</param>
/// <param name="Field">The Field of the Release this Filter matches against</param>
/// <param name="Values">The values matched case-insensitively against the rendered Field</param>
/// <param name="Mode">The Mode deciding how a match affects the Release</param>
/// <param name="Tier">The Tier of this Filter, only meaningful for <see cref="FilterMode.PREFER" />; lower is more preferred</param>
public record ReleaseFilter(
	int Id,
	FilterField Field,
	IReadOnlyList<string> Values,
	FilterMode Mode,
	int Tier = 0);
