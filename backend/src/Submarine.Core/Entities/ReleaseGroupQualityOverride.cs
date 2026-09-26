using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     Quality source override for releases of a specific group when parsing finds no quality.
/// </summary>
public sealed class ReleaseGroupQualityOverride : Entity
{
	/// <summary>Release group name, unique.</summary>
	public string ReleaseGroup { get; set; } = string.Empty;

	/// <summary>Quality source assumed for this group.</summary>
	public QualitySource Source { get; set; }
}
