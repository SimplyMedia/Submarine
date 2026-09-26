using Submarine.Core.Provider;

namespace Submarine.Core.Entities;

/// <summary>
///     A blocked release that must not be grabbed again.
/// </summary>
public sealed class BlocklistItem : Entity
{
	/// <summary>Release title.</summary>
	public string ReleaseTitle { get; set; } = string.Empty;

	/// <summary>Release guid, when the protocol provides one.</summary>
	public string? Guid { get; set; }

	/// <summary>Protocol of the release.</summary>
	public Protocol Protocol { get; set; }

	/// <summary>Indexer the release came from.</summary>
	public int? IndexerId { get; set; }

	/// <summary>Related series.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Related series.</summary>
	public Series? Series { get; set; }

	/// <summary>Related movie.</summary>
	public int? MovieId { get; set; }

	/// <summary>Related movie.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Indexer the release came from.</summary>
	public Indexer? Indexer { get; set; }

	/// <summary>Related episode ids.</summary>
	public List<int> EpisodeIds { get; set; } = [];

	/// <summary>Why the release was blocked.</summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>Release size in bytes.</summary>
	public long? Size { get; set; }

	/// <summary>UTC timestamp of the block.</summary>
	public DateTime Date { get; set; }
}
