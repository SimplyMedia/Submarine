namespace Submarine.Core.Entities;

/// <summary>
///     Per-version override of one season's monitored flag. Lets a facade change monitoring for a single selected
///     version without affecting sibling versions of the same series, which keep their inherited or own overridden
///     state.
/// </summary>
public sealed class MediaVersionSeasonMonitoring : Entity
{
	/// <summary>Version the override applies to.</summary>
	public int MediaVersionId { get; set; }

	/// <summary>Version the override applies to.</summary>
	public MediaVersion MediaVersion { get; set; } = null!;

	/// <summary>Season number the override applies to.</summary>
	public int SeasonNumber { get; set; }

	/// <summary>Effective monitored flag for every episode of this season on this version.</summary>
	public bool Monitored { get; set; }
}
