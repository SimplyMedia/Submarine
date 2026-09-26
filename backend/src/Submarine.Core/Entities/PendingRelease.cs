using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     An approved release held back by a delay or availability window.
/// </summary>
public sealed class PendingRelease : Entity
{
	/// <summary>Release title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Full release payload as JSON.</summary>
	public string Release { get; set; } = string.Empty;

	/// <summary>Related series.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Related series.</summary>
	public Series? Series { get; set; }

	/// <summary>Related movie.</summary>
	public int? MovieId { get; set; }

	/// <summary>Related movie.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Related episode ids.</summary>
	public List<int> EpisodeIds { get; set; } = [];

	/// <summary>Why the release is pending.</summary>
	public PendingReleaseReason Reason { get; set; }

	/// <summary>UTC timestamp when the release was added.</summary>
	public DateTime Added { get; set; }
}
