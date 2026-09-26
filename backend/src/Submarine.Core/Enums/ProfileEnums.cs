namespace Submarine.Core.Enums;

/// <summary>
///     Field a release filter matches against.
/// </summary>
public enum ReleaseFilterField
{
	/// <summary>Release group.</summary>
	RELEASE_GROUP,

	/// <summary>Indexer the release came from.</summary>
	INDEXER,

	/// <summary>Quality of the release.</summary>
	QUALITY,

	/// <summary>Language of the release.</summary>
	LANGUAGE,

	/// <summary>Quality source of the release.</summary>
	SOURCE
}

/// <summary>
///     How a release filter treats matching releases.
/// </summary>
public enum ReleaseFilterMode
{
	/// <summary>Only allow matching releases.</summary>
	ALLOW,

	/// <summary>Reject matching releases.</summary>
	BLOCK,

	/// <summary>Prefer matching releases by tier.</summary>
	PREFER
}

/// <summary>
///     Type of a custom format specification.
/// </summary>
public enum CustomFormatSpecificationType
{
	/// <summary>Regular expression on the release title.</summary>
	RELEASE_TITLE,

	/// <summary>Regular expression on the release group.</summary>
	RELEASE_GROUP,

	/// <summary>Language of the release.</summary>
	LANGUAGE,

	/// <summary>Quality source of the release.</summary>
	QUALITY_SOURCE,

	/// <summary>Resolution of the release.</summary>
	RESOLUTION,

	/// <summary>Streaming provider of the release.</summary>
	STREAMING_PROVIDER,

	/// <summary>Edition of the release.</summary>
	EDITION,

	/// <summary>Release flags of the release.</summary>
	RELEASE_FLAG,

	/// <summary>Protocol of the release.</summary>
	PROTOCOL,

	/// <summary>Hardcoded subtitles present.</summary>
	HARDCODED_SUBS,

	/// <summary>Size range in GB.</summary>
	SIZE,

	/// <summary>Year range.</summary>
	YEAR,

	/// <summary>Indexer flags of the release.</summary>
	INDEXER_FLAG
}
