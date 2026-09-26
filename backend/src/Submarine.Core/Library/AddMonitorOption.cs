namespace Submarine.Core.Library;

/// <summary>
///     Which episodes are monitored when a series is added.
/// </summary>
public enum AddMonitorOption
{
	/// <summary>Monitor every episode.</summary>
	ALL,

	/// <summary>Monitor episodes airing from now on.</summary>
	FUTURE,

	/// <summary>Monitor episodes that have not aired yet, including never aired ones.</summary>
	MISSING,

	/// <summary>Monitor episodes that already aired.</summary>
	EXISTING,

	/// <summary>Monitor only the pilot, S01E01.</summary>
	PILOT,

	/// <summary>Monitor every episode of the first season.</summary>
	FIRST_SEASON,

	/// <summary>Monitor every episode of the latest season with episodes.</summary>
	LATEST_SEASON,

	/// <summary>Monitor nothing.</summary>
	NONE,

	/// <summary>Monitor episodes with no air date yet, airing in the future, or aired within the last 90 days.</summary>
	RECENT,

	/// <summary>Leave season and episode monitored flags untouched.</summary>
	SKIP
}
