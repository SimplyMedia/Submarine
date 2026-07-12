namespace Submarine.Core.Library;

/// <summary>
///     The earliest point in a Movie's release lifecycle at which it is considered available for grabbing
/// </summary>
public enum MinimumAvailability
{
	/// <summary>
	///     The movie is available as soon as it is announced
	/// </summary>
	ANNOUNCED,

	/// <summary>
	///     The movie is available once it is in cinemas
	/// </summary>
	IN_CINEMAS,

	/// <summary>
	///     The movie is available once it has been released, e.g. digitally or physically
	/// </summary>
	RELEASED
}
