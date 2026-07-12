using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.History;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

/// <summary>
///     Computes aggregate statistics about the library
/// </summary>
public class StatsService
{
	private static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(30);

	private readonly SubmarineDatabaseContext _context;

	public StatsService(SubmarineDatabaseContext context)
		=> _context = context;

	public async Task<LibraryStats> GetStatsAsync(CancellationToken cancellationToken = default)
	{
		var seriesCount = await _context.Series.AsNoTracking().CountAsync(cancellationToken);
		var endedSeriesCount = await _context.Series.AsNoTracking()
			.CountAsync(s => s.Status == SeriesStatus.ENDED, cancellationToken);
		var continuingSeriesCount = await _context.Series.AsNoTracking()
			.CountAsync(s => s.Status == SeriesStatus.CONTINUING, cancellationToken);
		var movieCount = await _context.Movies.AsNoTracking().CountAsync(cancellationToken);
		var episodeCount = await _context.Episodes.AsNoTracking().CountAsync(cancellationToken);
		var episodeFileCount = await _context.EpisodeFiles.AsNoTracking().CountAsync(cancellationToken);
		var movieFileCount = await _context.MovieFiles.AsNoTracking().CountAsync(cancellationToken);

		var episodeFileSize = await _context.EpisodeFiles.AsNoTracking().SumAsync(f => f.Size, cancellationToken);
		var movieFileSize = await _context.MovieFiles.AsNoTracking().SumAsync(f => f.Size, cancellationToken);

		var rootFolders = await _context.RootFolders.AsNoTracking().ToListAsync(cancellationToken);
		var perRootFolder = rootFolders.Select(ToRootFolderStats).ToList();

		var historyCounts = await GetHistoryCountsAsync(cancellationToken);

		return new LibraryStats(seriesCount, endedSeriesCount, continuingSeriesCount, movieCount, episodeCount,
			episodeFileCount, movieFileCount, episodeFileSize + movieFileSize, perRootFolder, historyCounts);
	}

	private async Task<HistoryCounts> GetHistoryCountsAsync(CancellationToken cancellationToken)
	{
		// DateTimeOffset comparisons aren't server-translatable on the Sqlite provider, filter client-side
		var since = DateTimeOffset.UtcNow - HistoryWindow;
		var recent = (await _context.History.AsNoTracking().ToListAsync(cancellationToken))
			.Where(h => h.CreatedAt >= since)
			.ToList();

		return new HistoryCounts(
			recent.Count(h => h.Type == HistoryEventType.GRABBED),
			recent.Count(h => h.Type == HistoryEventType.IMPORTED),
			recent.Count(h => h.Type == HistoryEventType.FAILED));
	}

	private static RootFolderStats ToRootFolderStats(RootFolder rootFolder)
	{
		try
		{
			var drive = new DriveInfo(rootFolder.Path);

			return new RootFolderStats(rootFolder.Path, drive.AvailableFreeSpace, drive.TotalSize);
		}
		catch (IOException)
		{
			return new RootFolderStats(rootFolder.Path, null, null);
		}
		catch (ArgumentException)
		{
			return new RootFolderStats(rootFolder.Path, null, null);
		}
	}
}
