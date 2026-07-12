namespace Submarine.Mappings.Models;

/// <summary>
///     AniList arc/season mapping database entity
/// </summary>
public class AniListMapping
{
	public int Id { get; set; }

	public int AniListId { get; set; }

	public int TvdbId { get; set; }

	public string Title { get; set; } = "";

	public int TvdbSeason { get; set; }

	public int EpisodeStart { get; set; }

	public int? EpisodeCount { get; set; }

	public int AbsoluteOffset { get; set; }
}
