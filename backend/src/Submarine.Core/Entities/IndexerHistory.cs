using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     One indexer request history entry.
/// </summary>
public sealed class IndexerHistory : Entity
{
	/// <summary>Id of the indexer.</summary>
	public int IndexerId { get; set; }

	/// <summary>The indexer this entry belongs to.</summary>
	public Indexer Indexer { get; set; } = null!;

	/// <summary>Event type.</summary>
	public IndexerHistoryEventType EventType { get; set; }

	/// <summary>Whether the request succeeded.</summary>
	public bool Successful { get; set; }

	/// <summary>Query performed.</summary>
	public string? Query { get; set; }

	/// <summary>Categories queried.</summary>
	public string? Categories { get; set; }

	/// <summary>Source that triggered the request.</summary>
	public string? Source { get; set; }

	/// <summary>Request duration in milliseconds.</summary>
	public int? ElapsedMs { get; set; }

	/// <summary>UTC timestamp of the request.</summary>
	public DateTime Date { get; set; }
}
