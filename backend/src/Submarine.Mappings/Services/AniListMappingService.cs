using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Data;

namespace Submarine.Mappings.Services;

/// <summary>
/// Resolves between AniList episode numbers and TVDB season and episode numbers.
/// One AniList entry covers one arc or cour of episodes.
/// </summary>
public sealed class AniListMappingService(MappingsDbContext db)
{
	/// <summary>
	/// Maps an AniList episode relative to one entry onto a TVDB episode.
	/// Returns null when the entry is unmapped, the episode is below one or beyond the entry length.
	/// </summary>
	public async Task<TvdbResolution?> ResolveTvdbAsync(int aniListId, int episode, CancellationToken cancellationToken = default)
	{
		var mapping = await db.AniListMappings.FirstOrDefaultAsync(m => m.AniListId == aniListId, cancellationToken);
		if (mapping is null || episode < 1 || (mapping.EpisodeCount is not null && episode > mapping.EpisodeCount))
		{
			return null;
		}

		return new TvdbResolution(
			mapping.TvdbId,
			mapping.TvdbSeason,
			mapping.EpisodeStart + episode - 1,
			episode + mapping.AbsoluteOffset);
	}

	/// <summary>
	/// Maps a TVDB episode onto the AniList entry covering it and the episode relative to that entry.
	/// Returns null when no entry covers the episode.
	/// </summary>
	public async Task<AniListResolution?> ResolveAniListAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
	{
		var mappings = await db.AniListMappings
			.Where(m => m.TvdbId == tvdbId && m.TvdbSeason == season)
			.OrderBy(m => m.EpisodeStart)
			.ToListAsync(cancellationToken);
		var match = mappings.FirstOrDefault(m =>
			m.EpisodeStart <= episode && (m.EpisodeCount is null || episode < m.EpisodeStart + m.EpisodeCount));
		if (match is null)
		{
			return null;
		}

		return new AniListResolution(match.AniListId, episode - match.EpisodeStart + 1);
	}

	/// <summary>
	/// Returns all entries for a TVDB series ordered by season and start episode.
	/// </summary>
	public async Task<IReadOnlyList<AniListMappingResource>> GetForSeriesAsync(int tvdbId, CancellationToken cancellationToken = default)
	{
		var mappings = await db.AniListMappings
			.Where(m => m.TvdbId == tvdbId)
			.OrderBy(m => m.TvdbSeason).ThenBy(m => m.EpisodeStart)
			.ToListAsync(cancellationToken);
		return mappings.Select(ToResource).ToList();
	}

	public static AniListMappingResource ToResource(Entities.AniListMapping mapping) => new(
		mapping.Id,
		mapping.AniListId,
		mapping.TvdbId,
		mapping.Title,
		mapping.TvdbSeason,
		mapping.EpisodeStart,
		mapping.EpisodeCount,
		mapping.AbsoluteOffset);
}
