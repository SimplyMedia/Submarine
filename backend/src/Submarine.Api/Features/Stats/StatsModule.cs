using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Health;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Stats;

/// <summary>
///     Library statistics, cached for thirty seconds.
/// </summary>
public sealed class StatsModule : IServiceModule, IEndpointModule
{
	private const string CacheKey = "submarine-stats";

	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddMemoryCache();

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapGet("/api/v1/stats", GetAsync);

	private static async Task<Ok<StatsDto>> GetAsync(
		SubmarineDbContext db,
		IMemoryCache cache,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var stats = await cache.GetOrCreateAsync(CacheKey, async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
			return await ComputeAsync(db, timeProvider, cancellationToken);
		});
		return TypedResults.Ok(stats);
	}

	private static async Task<StatsDto> ComputeAsync(
		SubmarineDbContext db,
		TimeProvider timeProvider,
		CancellationToken cancellationToken)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var since = now.AddDays(-30);

		var seriesStatuses = await db.Series
			.GroupBy(x => x.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

		var historyCounts = await db.HistoryEvents
			.Where(x => x.Date >= since)
			.GroupBy(x => x.Type)
			.Select(g => new { Type = g.Key, Count = g.Count() })
			.ToDictionaryAsync(g => g.Type, g => g.Count, cancellationToken);

		var episodeFileBytes = await db.EpisodeFiles.SumAsync(x => x.Size, cancellationToken);
		var movieFileBytes = await db.MovieFiles.SumAsync(x => x.Size, cancellationToken);

		var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(cancellationToken);
		var spaces = rootFolders
			.Select(folder => DiskSpace.Query(folder.Path) is { } space
				? new RootFolderSpaceDto(folder.Path, space.FreeBytes, space.TotalBytes)
				: new RootFolderSpaceDto(folder.Path, null, null))
			.ToList();

		var missingEpisodes = await db.Episodes
			.Where(x => x.Monitored
				&& !db.EpisodeFiles.Any(f => f.Episodes.Any(e => e.Id == x.Id)))
			.CountAsync(cancellationToken);

		var queueCount = await db.TrackedDownloads
			.CountAsync(x => x.State == TrackedDownloadState.DOWNLOADING
				|| x.State == TrackedDownloadState.IMPORT_PENDING
				|| x.State == TrackedDownloadState.IMPORTING
				|| x.State == TrackedDownloadState.FAILED_PENDING, cancellationToken);

		var episodeCount = await db.Episodes.CountAsync(cancellationToken);
		var episodeFileCount = await db.EpisodeFiles.CountAsync(cancellationToken);
		var movieFileCount = await db.MovieFiles.CountAsync(cancellationToken);
		var movieCount = await db.Movies.CountAsync(cancellationToken);

		return new StatsDto(
			seriesStatuses.Values.Sum(),
			seriesStatuses.GetValueOrDefault(SeriesStatus.CONTINUING),
			seriesStatuses.GetValueOrDefault(SeriesStatus.ENDED),
			movieCount,
			episodeCount,
			episodeFileCount,
			movieFileCount,
			missingEpisodes,
			episodeFileBytes + movieFileBytes,
			spaces,
			historyCounts.Select(pair => new HistoryCountDto(pair.Key.ToString(), pair.Value)).ToList(),
			queueCount);
	}
}

/// <summary>Free and total space of a root folder drive.</summary>
/// <param name="Path">Root folder path.</param>
/// <param name="FreeBytes">Free bytes of the drive, null when unavailable.</param>
/// <param name="TotalBytes">Total bytes of the drive, null when unavailable.</param>
public sealed record RootFolderSpaceDto(string Path, long? FreeBytes, long? TotalBytes);

/// <summary>History rows in the last 30 days grouped by event type.</summary>
/// <param name="Type">History event type.</param>
/// <param name="Count">Number of events.</param>
public sealed record HistoryCountDto(string Type, int Count);

/// <summary>Library statistics snapshot.</summary>
/// <param name="SeriesCount">Number of series.</param>
/// <param name="ContinuingCount">Series still airing.</param>
/// <param name="EndedCount">Series that finished airing.</param>
/// <param name="MovieCount">Number of movies.</param>
/// <param name="EpisodeCount">Number of episodes.</param>
/// <param name="EpisodeFileCount">Number of imported episode files.</param>
/// <param name="MovieFileCount">Number of imported movie files.</param>
/// <param name="MissingEpisodes">Monitored episodes without a file.</param>
/// <param name="TotalSizeBytes">Sum of all file sizes.</param>
/// <param name="RootFolders">Per root folder drive space.</param>
/// <param name="History30Days">History counts of the last 30 days by type.</param>
/// <param name="QueueCount">Downloads currently in the queue.</param>
public sealed record StatsDto(
	int SeriesCount,
	int ContinuingCount,
	int EndedCount,
	int MovieCount,
	int EpisodeCount,
	int EpisodeFileCount,
	int MovieFileCount,
	int MissingEpisodes,
	long TotalSizeBytes,
	IReadOnlyList<RootFolderSpaceDto> RootFolders,
	IReadOnlyList<HistoryCountDto> History30Days,
	int QueueCount);
