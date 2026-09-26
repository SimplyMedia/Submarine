using System;

namespace Submarine.Core.Release.Torrent;

/// <summary>
///     Flags for Torrent Releases
/// </summary>
[Flags]
public enum TorrentReleaseFlags
{
	/// <summary>
	///     No flags
	/// </summary>
	NONE = 0,

	/// <summary>
	///     Torrent is Freeleech (0% Download Credited)
	/// </summary>
	FREELEECH = 1 << 1,

	/// <summary>
	///     Torrent is Halfleech (50% Download Credited)
	/// </summary>
	HALFLEECH = 1 << 2,

	/// <summary>
	///     No Upload/Download is counted for this Torrent
	/// </summary>
	NEUTRALLEECH = 1 << 3,

	/// <summary>
	///     Double Upload is counted for this Torrent
	/// </summary>
	DOUBLE_UPLOAD = 1 << 4,

	/// <summary>
	///     Torrent has a Promotion (anything besides Freeleech, Halfleech, Double Upload)
	/// </summary>
	OTHER_PROMOTION = 1 << 5,

	/// <summary>
	///     Torrent is a Scene Release
	/// </summary>
	SCENE = 1 << 6,

	/// <summary>
	///     Torrent is an Internal Release for this Tracker
	/// </summary>
	INTERNAL = 1 << 7,

	/// <summary>
	///     This Torrent is Exclusive to this Tracker
	/// </summary>
	EXCLUSIVE = 1 << 8
}
