namespace Submarine.Core.Enums;

/// <summary>
///     Implementation of a metadata consumer, writing companion files and images next to imported media.
/// </summary>
public enum MetadataConsumerType
{
	/// <summary>Kodi/Emby compatible NFO files.</summary>
	KODI,

	/// <summary>Plex .plexmatch files.</summary>
	PLEX,

	/// <summary>Emby legacy movie.xml files.</summary>
	EMBY,

	/// <summary>Roksbox compatible XML and images.</summary>
	ROKSBOX,

	/// <summary>WDTV compatible XML and images.</summary>
	WDTV
}

/// <summary>
///     Type of an auto tagging rule specification.
/// </summary>
public enum AutoTaggingSpecificationType
{
	/// <summary>Genre of the series or movie.</summary>
	GENRE,

	/// <summary>Root folder used by one of the item's versions.</summary>
	ROOT_FOLDER,

	/// <summary>Series type, series only.</summary>
	SERIES_TYPE,

	/// <summary>Airing or release status.</summary>
	STATUS,

	/// <summary>Year range.</summary>
	YEAR,

	/// <summary>Quality profile used by one of the item's versions.</summary>
	QUALITY_PROFILE,

	/// <summary>Whether the item is monitored.</summary>
	MONITORED,

	/// <summary>Network (series) or studio (movie).</summary>
	NETWORK_OR_STUDIO
}
