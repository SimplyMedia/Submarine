namespace Submarine.Mappings.Models;

/// <summary>
///     Scene naming mapping database entity
/// </summary>
public class SceneMapping
{
	public int Id { get; set; }

	public int TvdbId { get; set; }

	public string Title { get; set; } = "";

	public int? SeasonNumber { get; set; }

	public int? SceneSeasonNumber { get; set; }

	public int EpisodeOffset { get; set; }
}
