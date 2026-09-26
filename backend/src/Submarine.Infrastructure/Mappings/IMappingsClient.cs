using Submarine.Contracts.Mappings;

namespace Submarine.Infrastructure.Mappings;

/// <summary>
///     Typed client for the Submarine.Mappings service.
/// </summary>
public interface IMappingsClient
{
	/// <summary>All scene mappings of a TVDB series, empty when unmapped.</summary>
	Task<SceneMappingSetResource> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>Map a TVDB episode into scene numbering. Passthrough when unmapped.</summary>
	Task<SceneResolution> ResolveSceneAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default);

	/// <summary>Map a scene episode back to TVDB numbering. Passthrough when unmapped.</summary>
	Task<TvdbResolution> ResolveTvdbAsync(int tvdbId, int sceneSeason, int sceneEpisode, CancellationToken cancellationToken = default);

	/// <summary>Alternate scene titles of a TVDB series.</summary>
	Task<IReadOnlyList<SceneNameResource>> GetSceneNamesAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>TVDB ids known under a scene name, case-insensitive.</summary>
	Task<IReadOnlyList<int>> FindByNameAsync(string name, CancellationToken cancellationToken = default);

	/// <summary>AniList arcs of a TVDB series, empty when unmapped.</summary>
	Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>Map a TVDB episode into AniList numbering.</summary>
	Task<AniListResolution> ResolveAniListAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default);
}
