namespace Submarine.Core.Library;

/// <summary>
///     Which Episodes of a <see cref="Series" /> are monitored when it is added
/// </summary>
public enum MonitorOption
{
	/// <summary>
	///     Monitor every Episode
	/// </summary>
	ALL,

	/// <summary>
	///     Monitor only Episodes which have not aired yet
	/// </summary>
	FUTURE,

	/// <summary>
	///     Monitor Episodes which have aired but are missing a file
	/// </summary>
	MISSING,

	/// <summary>
	///     Monitor Episodes which have aired and are already present, i.e. the inverse of <see cref="MISSING" />
	/// </summary>
	EXISTING,

	/// <summary>
	///     Monitor only the pilot Episode (S01E01)
	/// </summary>
	PILOT,

	/// <summary>
	///     Monitor only the first Season
	/// </summary>
	FIRST_SEASON,

	/// <summary>
	///     Monitor only the latest Season
	/// </summary>
	LATEST_SEASON,

	/// <summary>
	///     Monitor no Episodes
	/// </summary>
	NONE
}
