using Submarine.Mappings.Contracts;

namespace Submarine.Api.Clients;

/// <summary>
///     Client for resolving scene and AniList numbering from the Mappings service
/// </summary>
public interface IMappingsClient
{
	/// <summary>
	///     Gets all scene mappings for a series, including per-episode overrides
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>scene mapping set, or null when the series has no mappings</returns>
	Task<SceneMappingSet?> GetSceneMappingsAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets all AniList arc mappings for a series
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>AniList mappings, empty when the series has none</returns>
	Task<IReadOnlyList<AniListMappingResource>> GetAniListMappingsAsync(int tvdbId,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Resolves the AniList numbering for a TVDB season episode
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="season">TVDB season number</param>
	/// <param name="episode">TVDB season-relative episode number</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>AniList numbering, or null when no entry covers the episode</returns>
	Task<AniListResolution?> ResolveAniListAsync(int tvdbId, int season, int episode,
		CancellationToken cancellationToken = default);
}

/// <summary>
///     All scene mappings for a series, including per-episode overrides
/// </summary>
/// <param name="Mappings">season-level (or all-seasons) mappings</param>
/// <param name="EpisodeMappings">per-episode overrides</param>
public record SceneMappingSet(
	IReadOnlyList<SceneMappingResource> Mappings,
	IReadOnlyList<SceneEpisodeMappingResource> EpisodeMappings);

/// <summary>
///     Resolved AniList numbering for a TVDB episode
/// </summary>
/// <param name="AniListId">AniList identifier of the entry</param>
/// <param name="AniListEpisode">episode number within the AniList entry</param>
public record AniListResolution(int AniListId, int AniListEpisode);
