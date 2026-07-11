namespace Submarine.Core.Library;

/// <summary>
///     Status of a <see cref="Series" />
/// </summary>
public enum SeriesStatus
{
	/// <summary>
	///     The Series is still continuing and airing new Episodes
	/// </summary>
	CONTINUING,

	/// <summary>
	///     The Series has ended
	/// </summary>
	ENDED,

	/// <summary>
	///     The Series has not aired yet
	/// </summary>
	UPCOMING,

	/// <summary>
	///     The status of the Series is unknown
	/// </summary>
	UNKNOWN
}
