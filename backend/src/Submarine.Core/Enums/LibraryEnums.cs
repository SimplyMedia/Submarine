namespace Submarine.Core.Enums;

/// <summary>
///     Kind of media a root folder or import list manages.
/// </summary>
public enum MediaKind
{
	/// <summary>TV series.</summary>
	SERIES,

	/// <summary>Movies.</summary>
	MOVIES
}

/// <summary>
///     Airing status of a series.
/// </summary>
public enum SeriesStatus
{
	/// <summary>Still airing new episodes.</summary>
	CONTINUING,

	/// <summary>Finished airing.</summary>
	ENDED,

	/// <summary>Not yet aired.</summary>
	UPCOMING,

	/// <summary>Unknown status.</summary>
	UNKNOWN
}

/// <summary>
///     Episode numbering style of a series.
/// </summary>
public enum SeriesType
{
	/// <summary>Standard S/E numbered series.</summary>
	STANDARD,

	/// <summary>Daily dated series.</summary>
	DAILY,

	/// <summary>Absolute numbered anime.</summary>
	ANIME
}

/// <summary>
///     Metadata provider used for a series.
/// </summary>
public enum MetadataProvider
{
	/// <summary>TheTVDB.</summary>
	TVDB,

	/// <summary>TMDB.</summary>
	TMDB
}

/// <summary>
///     Episode numbering scheme of a series.
/// </summary>
public enum SeriesNumbering
{
	/// <summary>Per aired season and episode.</summary>
	AIRED,

	/// <summary>DVD order.</summary>
	DVD,

	/// <summary>Absolute episode numbers.</summary>
	ABSOLUTE
}

/// <summary>
///     Whether new items are monitored automatically.
/// </summary>
public enum MonitorNewItems
{
	/// <summary>Monitor all new items.</summary>
	ALL,

	/// <summary>Do not monitor new items.</summary>
	NONE
}

/// <summary>
///     Release status of a movie.
/// </summary>
public enum MovieStatus
{
	/// <summary>Announced, not yet in cinemas.</summary>
	ANNOUNCED,

	/// <summary>Currently in cinemas.</summary>
	IN_CINEMAS,

	/// <summary>Released on home media.</summary>
	RELEASED
}

/// <summary>
///     Earliest availability a movie must reach before it is grabbed.
/// </summary>
public enum MinimumAvailability
{
	/// <summary>As soon as announced.</summary>
	ANNOUNCED,

	/// <summary>Once in cinemas.</summary>
	IN_CINEMAS,

	/// <summary>Once released on home media.</summary>
	RELEASED
}
