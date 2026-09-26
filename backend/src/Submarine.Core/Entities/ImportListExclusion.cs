namespace Submarine.Core.Entities;

/// <summary>
///     A library id excluded from import list syncs.
/// </summary>
public sealed class ImportListExclusion : Entity
{
	/// <summary>TheTVDB id, for series exclusions.</summary>
	public int? TvdbId { get; set; }

	/// <summary>TMDB id, for movie exclusions.</summary>
	public int? TmdbId { get; set; }

	/// <summary>Title for display.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Release year.</summary>
	public int? Year { get; set; }
}
