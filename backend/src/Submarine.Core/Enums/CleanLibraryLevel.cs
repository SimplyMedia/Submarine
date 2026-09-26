namespace Submarine.Core.Enums;

/// <summary>
///     What to do with library items no longer covered by any automatic-add import list, checked
///     at the end of a full import list sync.
/// </summary>
public enum CleanLibraryLevel
{
	/// <summary>Do nothing.</summary>
	DISABLED,

	/// <summary>Only log which items would be affected.</summary>
	LOG_ONLY,

	/// <summary>Keep the item in the library but unmonitor it.</summary>
	KEEP_AND_UNMONITOR,

	/// <summary>Remove the item from the library, keeping its files on disk.</summary>
	REMOVE_AND_KEEP,

	/// <summary>Remove the item from the library and delete its files.</summary>
	REMOVE_AND_DELETE
}
