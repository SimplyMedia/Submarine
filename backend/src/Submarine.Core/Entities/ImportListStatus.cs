namespace Submarine.Core.Entities;

/// <summary>
///     Runtime sync and failure state of an import list, one row per list.
/// </summary>
public sealed class ImportListStatus : Entity
{
	/// <summary>Id of the import list, unique.</summary>
	public int ImportListId { get; set; }

	/// <summary>The import list this status belongs to.</summary>
	public ImportList ImportList { get; set; } = null!;

	/// <summary>UTC timestamp of the last successful fetch.</summary>
	public DateTime? LastSyncAt { get; set; }

	/// <summary>UTC timestamp until which the list is disabled by backoff.</summary>
	public DateTime? DisabledUntil { get; set; }

	/// <summary>UTC timestamp of the first failure in the current escalation window.</summary>
	public DateTime? InitialFailure { get; set; }

	/// <summary>UTC timestamp of the most recent failure.</summary>
	public DateTime? MostRecentFailure { get; set; }

	/// <summary>Current backoff escalation level.</summary>
	public int EscalationLevel { get; set; }
}
