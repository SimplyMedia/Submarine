namespace Submarine.Core.Entities;

/// <summary>
///     An alternative or scene title of a series or movie.
/// </summary>
public sealed class AlternativeTitle : Entity
{
	/// <summary>Id of the series, if this title belongs to one.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Id of the movie, if this title belongs to one.</summary>
	public int? MovieId { get; set; }

	/// <summary>The title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Scene season number this title applies to.</summary>
	public int? SceneSeasonNumber { get; set; }
}
