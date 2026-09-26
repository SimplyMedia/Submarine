using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Resolves and mutates the effective monitored state of one episode on one media version, without affecting
///     sibling versions of the same series. Precedence, most specific first: a per-version episode override, a
///     per-version season override, then the version's own monitored flag combined with the episode and series'
///     inherited monitoring. With no override at all, the effective state is identical to native title-wide
///     monitoring, so introducing this overlay never changes behavior for a title with a single version.
/// </summary>
public sealed class VersionMonitoringService(SubmarineDbContext db, IEventBus eventBus)
{
	/// <summary>Resolves the effective monitored flag of one episode on one media version.</summary>
	public async Task<bool> IsEpisodeMonitoredAsync(int episodeId, int mediaVersionId, CancellationToken cancellationToken = default)
	{
		var episode = await db.Episodes.AsNoTracking().Include(x => x.Series)
			.FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken)
			?? throw new KeyNotFoundException($"Episode {episodeId} not found");
		var overrides = await LoadOverridesAsync(mediaVersionId, [episode.SeasonNumber], [episodeId], cancellationToken);
		return Effective(episode, overrides.VersionMonitored, overrides.Seasons, overrides.Episodes);
	}

	/// <summary>Resolves the effective monitored flag of many episodes on one media version, in one batch.</summary>
	public async Task<IReadOnlyDictionary<int, bool>> GetEffectiveMonitoringAsync(
		IReadOnlyList<int> episodeIds, int mediaVersionId, CancellationToken cancellationToken = default)
	{
		if (episodeIds.Count == 0)
		{
			return new Dictionary<int, bool>();
		}

		var episodes = await db.Episodes.AsNoTracking().Include(x => x.Series)
			.Where(x => episodeIds.Contains(x.Id)).ToListAsync(cancellationToken);
		var overrides = await LoadOverridesAsync(
			mediaVersionId, [.. episodes.Select(x => x.SeasonNumber).Distinct()], episodeIds, cancellationToken);
		return episodes.ToDictionary(x => x.Id, x => Effective(x, overrides.VersionMonitored, overrides.Seasons, overrides.Episodes));
	}

	/// <summary>
	///     Sets or clears the per-version override for one episode. A null value removes the override, restoring
	///     the season override or inherited state for that episode on this version only.
	/// </summary>
	public async Task SetEpisodeOverrideAsync(int mediaVersionId, int episodeId, bool? monitored, CancellationToken cancellationToken = default)
	{
		await UpsertEpisodeOverrideAsync(mediaVersionId, episodeId, monitored, cancellationToken);
		await eventBus.PublishAsync(new EpisodeUpdatedEvent(episodeId), cancellationToken);
	}

	/// <summary>Sets or clears the per-version override for many episodes at once.</summary>
	public async Task SetEpisodesOverrideAsync(int mediaVersionId, IReadOnlyList<int> episodeIds, bool? monitored, CancellationToken cancellationToken = default)
	{
		foreach (var episodeId in episodeIds)
		{
			await UpsertEpisodeOverrideAsync(mediaVersionId, episodeId, monitored, cancellationToken);
		}

		foreach (var episodeId in episodeIds)
		{
			await eventBus.PublishAsync(new EpisodeUpdatedEvent(episodeId), cancellationToken);
		}
	}

	/// <summary>
	///     Sets or clears the per-version override for a whole season. A null value removes the override, restoring
	///     inherited state for every episode of that season on this version only, unless a more specific per-episode
	///     override exists.
	/// </summary>
	public async Task SetSeasonOverrideAsync(int mediaVersionId, int seasonNumber, bool? monitored, CancellationToken cancellationToken = default)
	{
		var existing = await db.MediaVersionSeasonMonitorings.FirstOrDefaultAsync(
			x => x.MediaVersionId == mediaVersionId && x.SeasonNumber == seasonNumber, cancellationToken);
		if (monitored is null)
		{
			if (existing is not null)
			{
				db.MediaVersionSeasonMonitorings.Remove(existing);
			}
		}
		else if (existing is null)
		{
			db.MediaVersionSeasonMonitorings.Add(new MediaVersionSeasonMonitoring
			{
				MediaVersionId = mediaVersionId,
				SeasonNumber = seasonNumber,
				Monitored = monitored.Value
			});
		}
		else
		{
			existing.Monitored = monitored.Value;
		}

		await db.SaveChangesAsync(cancellationToken);

		var episodeOverrideIds = await db.MediaVersionEpisodeMonitorings.AsNoTracking()
			.Where(x => x.MediaVersionId == mediaVersionId)
			.Select(x => x.EpisodeId)
			.ToListAsync(cancellationToken);
		var episodeIds = await db.Episodes.AsNoTracking()
			.Where(x => x.SeasonNumber == seasonNumber && !episodeOverrideIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);
		foreach (var episodeId in episodeIds)
		{
			await eventBus.PublishAsync(new EpisodeUpdatedEvent(episodeId), cancellationToken);
		}
	}

	/// <summary>
	///     Applies override precedence: a per-version episode override wins, then a per-version season override,
	///     then the inherited monitored state.
	/// </summary>
	public static bool Resolve(bool inherited, bool? seasonOverride, bool? episodeOverride)
		=> episodeOverride ?? seasonOverride ?? inherited;

	private static bool Effective(
		Episode episode,
		bool versionMonitored,
		IReadOnlyDictionary<int, bool> seasonOverrides,
		IReadOnlyDictionary<int, bool> episodeOverrides)
		=> Resolve(
			versionMonitored && episode.Monitored && episode.Series.Monitored,
			seasonOverrides.TryGetValue(episode.SeasonNumber, out var seasonOverride) ? seasonOverride : null,
			episodeOverrides.TryGetValue(episode.Id, out var episodeOverride) ? episodeOverride : null);

	private async Task<(bool VersionMonitored, IReadOnlyDictionary<int, bool> Seasons, IReadOnlyDictionary<int, bool> Episodes)> LoadOverridesAsync(
		int mediaVersionId, IReadOnlyList<int> seasonNumbers, IReadOnlyList<int> episodeIds, CancellationToken cancellationToken)
	{
		var versionMonitored = await db.MediaVersions.AsNoTracking()
			.Where(x => x.Id == mediaVersionId).Select(x => (bool?)x.Monitored).FirstOrDefaultAsync(cancellationToken)
			?? throw new KeyNotFoundException($"Media version {mediaVersionId} not found");
		var seasons = await db.MediaVersionSeasonMonitorings.AsNoTracking()
			.Where(x => x.MediaVersionId == mediaVersionId && seasonNumbers.Contains(x.SeasonNumber))
			.ToDictionaryAsync(x => x.SeasonNumber, x => x.Monitored, cancellationToken);
		var episodes = await db.MediaVersionEpisodeMonitorings.AsNoTracking()
			.Where(x => x.MediaVersionId == mediaVersionId && episodeIds.Contains(x.EpisodeId))
			.ToDictionaryAsync(x => x.EpisodeId, x => x.Monitored, cancellationToken);
		return (versionMonitored, seasons, episodes);
	}

	private async Task UpsertEpisodeOverrideAsync(int mediaVersionId, int episodeId, bool? monitored, CancellationToken cancellationToken)
	{
		var existing = await db.MediaVersionEpisodeMonitorings.FirstOrDefaultAsync(
			x => x.MediaVersionId == mediaVersionId && x.EpisodeId == episodeId, cancellationToken);
		if (monitored is null)
		{
			if (existing is not null)
			{
				db.MediaVersionEpisodeMonitorings.Remove(existing);
			}
		}
		else if (existing is null)
		{
			db.MediaVersionEpisodeMonitorings.Add(new MediaVersionEpisodeMonitoring
			{
				MediaVersionId = mediaVersionId,
				EpisodeId = episodeId,
				Monitored = monitored.Value
			});
		}
		else
		{
			existing.Monitored = monitored.Value;
		}

		await db.SaveChangesAsync(cancellationToken);
	}
}
