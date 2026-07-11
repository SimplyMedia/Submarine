namespace Submarine.Mappings.Contracts;

/// <summary>
///     A scene naming mapping for a series or a single season within a series
/// </summary>
/// <param name="TvdbId">TheTVDB identifier of the series this mapping applies to</param>
/// <param name="Title">scene title variant of the series</param>
/// <param name="SeasonNumber">season this mapping applies to, or null if it applies to all seasons</param>
/// <param name="SceneSeasonNumber">season number used by the scene release</param>
/// <param name="EpisodeOffset">offset to apply to the episode number to resolve the scene numbering</param>
public record SceneMappingResource(
	int TvdbId,
	string Title,
	int? SeasonNumber,
	int? SceneSeasonNumber,
	int EpisodeOffset = 0);
