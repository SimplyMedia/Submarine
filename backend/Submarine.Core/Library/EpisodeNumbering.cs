namespace Submarine.Core.Library;

/// <summary>
///     Episode numbering scheme used to materialize the episodes of a <see cref="Series" />
/// </summary>
public enum EpisodeNumbering
{
	/// <summary>
	///     Aired order
	/// </summary>
	AIRED,

	/// <summary>
	///     DVD order
	/// </summary>
	DVD,

	/// <summary>
	///     Absolute order, using the absolute episode number within a single season
	/// </summary>
	ABSOLUTE
}
