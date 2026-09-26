namespace Submarine.Contracts.Mappings;

/// <summary>
/// Result of mapping a TVDB episode into the scene numbering.
/// </summary>
public sealed record SceneResolution(int SceneSeason, int SceneEpisode);

/// <summary>
/// Result of resolving an AniList episode or a scene episode back to a TVDB episode.
/// AbsoluteEpisode is only populated by AniList resolution.
/// </summary>
public sealed record TvdbResolution(int TvdbId, int Season, int Episode, int AbsoluteEpisode);

/// <summary>
/// Result of mapping a TVDB episode into AniList numbering.
/// </summary>
public sealed record AniListResolution(int AniListId, int AniListEpisode);
