using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;

namespace Submarine.Mappings.Services;

/// <summary>
///     Resolves scene numbering from TVDB numbering and vice versa, applying per-episode overrides before
///     falling back to the season-level offset
/// </summary>
public class MappingResolver
{
	private readonly MappingsDatabaseContext _context;

	/// <summary>
	///     Creates a new instance of <see cref="MappingResolver" />
	/// </summary>
	/// <param name="context">database context to resolve mappings from</param>
	public MappingResolver(MappingsDatabaseContext context)
		=> _context = context;

	/// <summary>
	///     Resolves the scene numbering for a TVDB episode
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="season">TVDB season number</param>
	/// <param name="episode">TVDB episode number</param>
	/// <returns>scene season and episode number</returns>
	public async Task<(int SeasonNumber, int EpisodeNumber)> ResolveSceneAsync(int tvdbId, int season, int episode)
	{
		var episodeOverride = await _context.SceneEpisodeMappings.AsNoTracking()
			.FirstOrDefaultAsync(m => m.TvdbId == tvdbId && m.SeasonNumber == season && m.EpisodeNumber == episode);

		if (episodeOverride != null)
			return (episodeOverride.SceneSeasonNumber, episodeOverride.SceneEpisodeNumber);

		var mapping = await _context.SceneMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId && (m.SeasonNumber == season || m.SeasonNumber == null))
			.OrderByDescending(m => m.SeasonNumber != null)
			.FirstOrDefaultAsync();

		if (mapping == null)
			return (season, episode);

		return (mapping.SceneSeasonNumber ?? season, episode + mapping.EpisodeOffset);
	}

	/// <summary>
	///     Resolves the TVDB numbering for a scene episode
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="sceneSeason">scene season number</param>
	/// <param name="sceneEpisode">scene episode number</param>
	/// <returns>TVDB season and episode number</returns>
	public async Task<(int SeasonNumber, int EpisodeNumber)> ResolveTvdbAsync(int tvdbId, int sceneSeason,
		int sceneEpisode)
	{
		var episodeOverride = await _context.SceneEpisodeMappings.AsNoTracking()
			.FirstOrDefaultAsync(m =>
				m.TvdbId == tvdbId && m.SceneSeasonNumber == sceneSeason && m.SceneEpisodeNumber == sceneEpisode);

		if (episodeOverride != null)
			return (episodeOverride.SeasonNumber, episodeOverride.EpisodeNumber);

		var mapping = await _context.SceneMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId && (m.SceneSeasonNumber == sceneSeason || m.SceneSeasonNumber == null))
			.OrderByDescending(m => m.SceneSeasonNumber != null)
			.FirstOrDefaultAsync();

		if (mapping == null)
			return (sceneSeason, sceneEpisode);

		return (mapping.SeasonNumber ?? sceneSeason, sceneEpisode - mapping.EpisodeOffset);
	}
}
