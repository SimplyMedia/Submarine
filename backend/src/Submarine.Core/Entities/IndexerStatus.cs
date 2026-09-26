namespace Submarine.Core.Entities;

/// <summary>
///     Runtime failure state of an indexer, one row per indexer.
/// </summary>
public sealed class IndexerStatus : Entity
{
	/// <summary>Id of the indexer, unique.</summary>
	public int IndexerId { get; set; }

	/// <summary>The indexer this status belongs to.</summary>
	public Indexer Indexer { get; set; } = null!;

	/// <summary>UTC timestamp until which the indexer is disabled by backoff.</summary>
	public DateTime? DisabledUntil { get; set; }

	/// <summary>UTC timestamp of the first failure in the current escalation window.</summary>
	public DateTime? InitialFailure { get; set; }

	/// <summary>UTC timestamp of the most recent failure.</summary>
	public DateTime? MostRecentFailure { get; set; }

	/// <summary>Current backoff escalation level.</summary>
	public int EscalationLevel { get; set; }

	/// <summary>Release info of the last RSS sync item, used for duplicate suppression.</summary>
	public string? LastRssSyncReleaseInfo { get; set; }
}
