using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Commands;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Indexers;
using Submarine.Core.Parser;
using Submarine.Core.Profiles;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Core.Release.Usenet;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Grab;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Re-decides every pending release; grabs it once approved (delay window cleared or availability reached) or
///     drops it once permanently rejected (e.g. blocklisted while pending).
/// </summary>
public sealed class ProcessPendingReleasesCommandHandler(
	SubmarineDbContext db,
	DecisionContextFactory contextFactory,
	IDownloadDecisionMaker decisionMaker,
	IGrabService grabService,
	IParser<TorrentRelease> torrentParser,
	IParser<UsenetRelease> usenetParser,
	TimeProvider timeProvider,
	ILogger<ProcessPendingReleasesCommandHandler> logger) : ICommandHandler<ProcessPendingReleasesCommand>
{
	private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

	/// <inheritdoc />
	public async Task ExecuteAsync(ProcessPendingReleasesCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		await PruneAsync(cancellationToken);

		var pendingReleases = await db.PendingReleases.ToListAsync(cancellationToken);

		foreach (var pending in pendingReleases)
		{
			var info = JsonSerializer.Deserialize<ReleaseInfo>(pending.Release);
			var parsed = info is null ? null : TryParse(info);
			if (info is null || parsed is null)
			{
				continue;
			}

			var candidate = new ReleaseCandidate(parsed, info, null, pending.SeriesId, pending.MovieId, pending.EpisodeIds);
			var versions = await ResolveVersionsAsync(pending, cancellationToken);
			var settled = false;

			foreach (var version in versions)
			{
				var decisionContext = await contextFactory.BuildAsync(version, pending.EpisodeIds, cancellationToken);
				var decision = decisionMaker.Decide(candidate, decisionContext);

				if (decision.Approved)
				{
					try
					{
						await grabService.GrabAsync(decision, version.Id, pending.SeriesId, pending.EpisodeIds, pending.MovieId, cancellationToken);
						settled = true;
					}
					catch (Exception exception)
					{
						logger.LogWarning(exception, "Grab of pending release {Title} failed", pending.Title);
					}

					break;
				}

				if (decision.Rejections.Count > 0 && decision.Rejections.Any(rejection => rejection.Type == RejectionType.PERMANENT))
				{
					settled = true;
					break;
				}
			}

			if (settled)
			{
				db.PendingReleases.Remove(pending);
				await db.SaveChangesAsync(cancellationToken);
			}
		}
	}

	// prunes rows that lingered past a reasonable wait (persistent TEMPORARY rejections never resolve on their own)
	// and rows whose target episodes or movie already have a file meeting the quality cutoff
	private async Task PruneAsync(CancellationToken cancellationToken)
	{
		var cutoff = timeProvider.GetUtcNow().UtcDateTime - MaxAge;
		var candidates = await db.PendingReleases.ToListAsync(cancellationToken);
		var toRemove = new List<Core.Entities.PendingRelease>();

		foreach (var pending in candidates)
		{
			if (pending.Added < cutoff || await IsSatisfiedAsync(pending, cancellationToken))
			{
				toRemove.Add(pending);
			}
		}

		if (toRemove.Count == 0)
		{
			return;
		}

		db.PendingReleases.RemoveRange(toRemove);
		await db.SaveChangesAsync(cancellationToken);
	}

	private async Task<bool> IsSatisfiedAsync(Core.Entities.PendingRelease pending, CancellationToken cancellationToken)
	{
		var versions = await ResolveVersionsAsync(pending, cancellationToken);

		foreach (var version in versions)
		{
			var profile = await db.QualityProfiles.AsNoTracking()
				.FirstOrDefaultAsync(profile => profile.Id == version.QualityProfileId, cancellationToken);
			if (profile is null)
			{
				continue;
			}

			if (pending.MovieId is not null)
			{
				var file = await db.MovieFiles.AsNoTracking().FirstOrDefaultAsync(file => file.MediaVersionId == version.Id, cancellationToken);
				if (file is not null && profile.MeetsCutoff(file.Quality))
				{
					return true;
				}

				continue;
			}

			if (pending.EpisodeIds.Count == 0)
			{
				continue;
			}

			var files = await db.EpisodeFiles.AsNoTracking()
				.Where(file => file.MediaVersionId == version.Id && file.Episodes.Any(episode => pending.EpisodeIds.Contains(episode.Id)))
				.ToListAsync(cancellationToken);

			var coveredIds = files.SelectMany(file => file.Episodes.Select(episode => episode.Id)).Distinct().ToHashSet();
			if (pending.EpisodeIds.All(coveredIds.Contains) && files.All(file => profile.MeetsCutoff(file.Quality)))
			{
				return true;
			}
		}

		return false;
	}

	private async Task<IReadOnlyList<Core.Entities.MediaVersion>> ResolveVersionsAsync(Core.Entities.PendingRelease pending, CancellationToken cancellationToken)
	{
		if (pending.SeriesId is { } seriesId)
		{
			return await db.MediaVersions.Where(version => version.SeriesId == seriesId).ToListAsync(cancellationToken);
		}

		if (pending.MovieId is { } movieId)
		{
			return await db.MediaVersions.Where(version => version.MovieId == movieId).ToListAsync(cancellationToken);
		}

		return [];
	}

	private BaseRelease? TryParse(ReleaseInfo info)
	{
		try
		{
			return info.Protocol == Protocol.USENET ? usenetParser.Parse(info.Title) : torrentParser.Parse(info.Title);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
