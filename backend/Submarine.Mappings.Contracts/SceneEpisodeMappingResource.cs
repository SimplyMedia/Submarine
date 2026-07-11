namespace Submarine.Mappings.Contracts;

/// <summary>
///     A per-episode override of a <see cref="SceneMappingResource" />
/// </summary>
/// <param name="TvdbId">TheTVDB identifier of the series this override applies to</param>
/// <param name="SeasonNumber">season number of the episode this override applies to</param>
/// <param name="EpisodeNumber">episode number this override applies to</param>
/// <param name="SceneSeasonNumber">season number used by the scene release for this episode</param>
/// <param name="SceneEpisodeNumber">episode number used by the scene release for this episode</param>
public record SceneEpisodeMappingResource(
	int TvdbId,
	int SeasonNumber,
	int EpisodeNumber,
	int SceneSeasonNumber,
	int SceneEpisodeNumber);
