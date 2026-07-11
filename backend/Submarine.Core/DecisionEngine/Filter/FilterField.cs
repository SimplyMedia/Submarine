namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     The field of a Release a <see cref="ReleaseFilter" /> matches against
/// </summary>
public enum FilterField
{
	/// <summary>
	///     The Release Group of the Release
	/// </summary>
	RELEASE_GROUP,

	/// <summary>
	///     The name of the Indexer the Release originates from
	/// </summary>
	INDEXER,

	/// <summary>
	///     The Quality of the Release
	/// </summary>
	QUALITY,

	/// <summary>
	///     A Language of the Release
	/// </summary>
	LANGUAGE,

	/// <summary>
	///     The Streaming Provider of the Release
	/// </summary>
	SOURCE
}
