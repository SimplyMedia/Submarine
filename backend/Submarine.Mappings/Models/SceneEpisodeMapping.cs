namespace Submarine.Mappings.Models;

/// <summary>
///     Per-episode scene naming mapping override database entity
/// </summary>
public class SceneEpisodeMapping
{
	public int Id { get; set; }

	public int TvdbId { get; set; }

	public int SeasonNumber { get; set; }

	public int EpisodeNumber { get; set; }

	public int SceneSeasonNumber { get; set; }

	public int SceneEpisodeNumber { get; set; }
}
