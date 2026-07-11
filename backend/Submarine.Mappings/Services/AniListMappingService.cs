using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;

namespace Submarine.Mappings.Services;

/// <summary>
///     Resolves TVDB numbering from AniList entry numbering and vice versa, accounting for AniList splitting
///     long series into one entry per arc/cour with per-entry absolute numbering
/// </summary>
public class AniListMappingService
{
	private readonly MappingsDatabaseContext _context;

	/// <summary>
	///     Creates a new instance of <see cref="AniListMappingService" />
	/// </summary>
	/// <param name="context">database context to resolve mappings from</param>
	public AniListMappingService(MappingsDatabaseContext context)
		=> _context = context;

	/// <summary>
	///     Resolves the TVDB numbering for an AniList episode
	/// </summary>
	/// <param name="aniListId">AniList identifier of the entry</param>
	/// <param name="aniListEpisode">episode number within the AniList entry, 1-based</param>
	/// <returns>TVDB numbering, or null when the entry is unmapped or the episode is outside its range</returns>
	public async Task<TvdbResolution?> ResolveTvdbAsync(int aniListId, int aniListEpisode)
	{
		var mapping = await _context.AniListMappings.AsNoTracking()
			.FirstOrDefaultAsync(m => m.AniListId == aniListId);

		if (mapping == null)
			return null;

		if (aniListEpisode < 1 || (mapping.EpisodeCount != null && aniListEpisode > mapping.EpisodeCount))
			return null;

		return new TvdbResolution(
			mapping.TvdbId,
			mapping.TvdbSeason,
			mapping.EpisodeStart + aniListEpisode - 1,
			aniListEpisode + mapping.AbsoluteOffset);
	}

	/// <summary>
	///     Resolves the AniList entry and episode for a TVDB season episode
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="season">TVDB season number</param>
	/// <param name="episode">TVDB season-relative episode number, 1-based</param>
	/// <returns>AniList numbering, or null when no entry covers the episode</returns>
	public async Task<AniListResolution?> ResolveAniListAsync(int tvdbId, int season, int episode)
	{
		var mappings = await _context.AniListMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId && m.TvdbSeason == season)
			.OrderBy(m => m.EpisodeStart)
			.ToListAsync();

		var mapping = mappings.FirstOrDefault(m =>
			episode >= m.EpisodeStart &&
			(m.EpisodeCount == null || episode < m.EpisodeStart + m.EpisodeCount));

		if (mapping == null)
			return null;

		return new AniListResolution(mapping.AniListId, episode - mapping.EpisodeStart + 1);
	}

	/// <summary>
	///     Gets all AniList mappings for a series
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	public async Task<IReadOnlyList<AniListMapping>> GetByTvdbAsync(int tvdbId)
		=> await _context.AniListMappings.AsNoTracking()
			.Where(m => m.TvdbId == tvdbId)
			.OrderBy(m => m.TvdbSeason)
			.ThenBy(m => m.EpisodeStart)
			.ToListAsync();
}

/// <summary>
///     Resolved TVDB numbering for an AniList episode
/// </summary>
/// <param name="TvdbId">TheTVDB identifier of the series</param>
/// <param name="Season">TVDB season number</param>
/// <param name="Episode">TVDB season-relative episode number</param>
/// <param name="AbsoluteEpisode">TVDB absolute episode number</param>
public record TvdbResolution(int TvdbId, int Season, int Episode, int AbsoluteEpisode);

/// <summary>
///     Resolved AniList numbering for a TVDB episode
/// </summary>
/// <param name="AniListId">AniList identifier of the entry</param>
/// <param name="AniListEpisode">episode number within the AniList entry</param>
public record AniListResolution(int AniListId, int AniListEpisode);
