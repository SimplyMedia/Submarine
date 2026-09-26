using Microsoft.EntityFrameworkCore;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Data;

namespace Submarine.Mappings.Services;

/// <summary>
/// Resolves TVDB episode numbers into scene numbering and back.
/// Per episode overrides win over season level mappings; an exact season match beats the all seasons wildcard.
/// </summary>
public sealed class MappingResolver(MappingsDbContext db)
{
	/// <summary>
	/// Maps a TVDB episode into scene numbering. Falls back to a passthrough when nothing is mapped.
	/// </summary>
	public async Task<SceneResolution> ResolveSceneAsync(int tvdbId, int season, int episode, CancellationToken cancellationToken = default)
	{
		var episodeOverride = await db.SceneEpisodeMappings
			.FirstOrDefaultAsync(m => m.TvdbId == tvdbId && m.SeasonNumber == season && m.EpisodeNumber == episode, cancellationToken);
		if (episodeOverride is not null)
		{
			return new SceneResolution(episodeOverride.SceneSeasonNumber, episodeOverride.SceneEpisodeNumber);
		}

		var mappings = await db.SceneMappings
			.Where(m => m.TvdbId == tvdbId)
			.ToListAsync(cancellationToken);
		var match = mappings
			.Where(m => m.SeasonNumber == season || m.SeasonNumber is null)
			.OrderByDescending(m => m.SeasonNumber is not null)
			.FirstOrDefault();
		if (match is null)
		{
			return new SceneResolution(season, episode);
		}

		return new SceneResolution(match.SceneSeasonNumber ?? season, episode + match.EpisodeOffset);
	}

	/// <summary>
	/// Maps a scene episode back into TVDB numbering. Falls back to a passthrough when nothing is mapped.
	/// </summary>
	public async Task<TvdbResolution> ResolveTvdbAsync(int tvdbId, int sceneSeason, int sceneEpisode, CancellationToken cancellationToken = default)
	{
		var episodeOverride = await db.SceneEpisodeMappings
			.FirstOrDefaultAsync(m => m.TvdbId == tvdbId && m.SceneSeasonNumber == sceneSeason && m.SceneEpisodeNumber == sceneEpisode, cancellationToken);
		if (episodeOverride is not null)
		{
			return new TvdbResolution(tvdbId, episodeOverride.SeasonNumber, episodeOverride.EpisodeNumber, 0);
		}

		var mappings = await db.SceneMappings
			.Where(m => m.TvdbId == tvdbId)
			.ToListAsync(cancellationToken);
		var match = mappings
			.Where(m => m.SceneSeasonNumber == sceneSeason || m.SceneSeasonNumber is null)
			.OrderByDescending(m => m.SceneSeasonNumber is not null)
			.FirstOrDefault();
		if (match is null)
		{
			return new TvdbResolution(tvdbId, sceneSeason, sceneEpisode, 0);
		}

		return new TvdbResolution(tvdbId, match.SeasonNumber ?? sceneSeason, sceneEpisode - match.EpisodeOffset, 0);
	}
}
