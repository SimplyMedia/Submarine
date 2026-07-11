namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     The mode of a <see cref="ReleaseFilter" />, deciding how a match affects a Release
/// </summary>
public enum FilterMode
{
	/// <summary>
	///     The Release must match one of the values or is rejected
	/// </summary>
	ALLOW,

	/// <summary>
	///     A matching Release is rejected
	/// </summary>
	BLOCK,

	/// <summary>
	///     A matching Release gets a tier-based score, without rejection
	/// </summary>
	PREFER
}
