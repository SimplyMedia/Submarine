namespace Submarine.Core.Entities;

/// <summary>
///     Per-version override of one episode's monitored flag. Takes precedence over a season override on the same
///     version, which takes precedence over the version's own monitored flag and the title/episode's inherited
///     monitoring.
/// </summary>
public sealed class MediaVersionEpisodeMonitoring : Entity
{
	/// <summary>Version the override applies to.</summary>
	public int MediaVersionId { get; set; }

	/// <summary>Version the override applies to.</summary>
	public MediaVersion MediaVersion { get; set; } = null!;

	/// <summary>Episode the override applies to.</summary>
	public int EpisodeId { get; set; }

	/// <summary>Episode the override applies to.</summary>
	public Episode Episode { get; set; } = null!;

	/// <summary>Effective monitored flag for this episode on this version.</summary>
	public bool Monitored { get; set; }
}
