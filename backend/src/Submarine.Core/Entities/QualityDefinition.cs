using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     Size limits for one quality. Seeded for every combination in QualityResolutionModel.All.
/// </summary>
public sealed class QualityDefinition : Entity
{
	/// <summary>Quality source, null for unknown.</summary>
	public QualitySource? Source { get; set; }

	/// <summary>Resolution, null for unknown.</summary>
	public QualityResolution? Resolution { get; set; }

	/// <summary>Display title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Minimum size in MB per minute of runtime.</summary>
	public double? MinSizeMbPerMinute { get; set; }

	/// <summary>Maximum size in MB per minute of runtime.</summary>
	public double? MaxSizeMbPerMinute { get; set; }

	/// <summary>Preferred size in MB per minute of runtime.</summary>
	public double? PreferredSizeMbPerMinute { get; set; }
}
