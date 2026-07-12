namespace Submarine.Core.DecisionEngine.CustomFormats;

/// <summary>
///     The Types of Custom Format Conditions
/// </summary>
public enum CustomFormatConditionType
{
	/// <summary>
	///     Regex match against the Full Title of the Release
	/// </summary>
	RELEASE_TITLE,

	/// <summary>
	///     Regex match against the Release Group of the Release
	/// </summary>
	RELEASE_GROUP,

	/// <summary>
	///     Exact match against the Languages of the Release
	/// </summary>
	LANGUAGE,

	/// <summary>
	///     Exact match against the Quality Source of the Release
	/// </summary>
	QUALITY_SOURCE,

	/// <summary>
	///     Exact match against the Resolution of the Release
	/// </summary>
	RESOLUTION,

	/// <summary>
	///     Exact match against the Streaming Provider of the Release
	/// </summary>
	STREAMING_PROVIDER,

	/// <summary>
	///     Regex match against the Edition of a Movie Release
	/// </summary>
	EDITION,

	/// <summary>
	///     Match against the Flags of a Torrent Release
	/// </summary>
	RELEASE_FLAG,

	/// <summary>
	///     Match against the Protocol of the Release
	/// </summary>
	PROTOCOL,

	/// <summary>
	///     Matches when the Release has Hardcoded Subs, Value is ignored
	/// </summary>
	HARDCODED_SUBS
}
