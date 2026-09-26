namespace Submarine.Contracts.Metadata;

/// <summary>
///     Numbering scheme an episode position refers to.
/// </summary>
public enum EpisodeOrdering
{
	AIRED,
	DVD,
	ABSOLUTE
}

/// <summary>
///     Lifecycle status of a series.
/// </summary>
public enum SeriesStatus
{
	CONTINUING,
	ENDED,
	UPCOMING,
	UNKNOWN
}

/// <summary>
///     Lifecycle status of a movie.
/// </summary>
public enum MovieStatus
{
	ANNOUNCED,
	IN_CINEMAS,
	RELEASED
}
