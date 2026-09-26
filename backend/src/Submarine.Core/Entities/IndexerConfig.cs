namespace Submarine.Core.Entities;

/// <summary>
///     Global indexer behaviour configuration, singleton row with Id 1.
/// </summary>
public sealed class IndexerConfig : SingletonEntity
{
	/// <summary>Minutes between RSS syncs, 0 disables the sync.</summary>
	public int RssSyncIntervalMinutes { get; set; } = 30;

	/// <summary>Minimum age in minutes for usenet releases.</summary>
	public int MinimumAgeMinutes { get; set; }

	/// <summary>Retention in days required for usenet releases, 0 disables.</summary>
	public int RetentionDays { get; set; }

	/// <summary>Maximum release size in MB, 0 disables.</summary>
	public int MaximumSizeMb { get; set; }

	/// <summary>Days a movie must be past its release date before grabbing, 0 disables.</summary>
	public int AvailabilityDelayDays { get; set; }
}
