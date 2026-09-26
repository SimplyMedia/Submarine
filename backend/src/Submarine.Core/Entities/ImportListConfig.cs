using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     Global import list behaviour configuration, singleton row with Id 1.
/// </summary>
public sealed class ImportListConfig : SingletonEntity
{
	/// <summary>
	///     What to do with library items no longer covered by any automatic-add import list. Only
	///     applied after a full sync where every automatic-add list synced successfully.
	/// </summary>
	public CleanLibraryLevel CleanLibraryLevel { get; set; } = CleanLibraryLevel.DISABLED;
}
