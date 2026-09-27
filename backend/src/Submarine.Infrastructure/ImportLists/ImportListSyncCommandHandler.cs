using Submarine.Core.Library;
using Submarine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Library;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Syncs one import list or all enabled ones: fetch, skip exclusions and existing
///     items, add the rest with the list settings. Lists with EnableAutomaticAdd off only report.
///     A full sync respects each list's minimum refresh interval and failure backoff, and, when
///     every automatic-add list synced successfully, applies the configured clean library level to
///     library items no longer covered by any of them.
/// </summary>
public sealed class ImportListSyncCommandHandler(
	SubmarineDbContext db,
	IEnumerable<IImportList> implementations,
	LibraryAdder adder,
	LibraryMutator mutator,
	IImportListStatusService statusService,
	ICommandQueue commandQueue,
	TimeProvider timeProvider)
	: ICommandHandler<ImportListSyncCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(ImportListSyncCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var isFullSync = command.ImportListId is null;
		var lists = await db.ImportLists
			.Include(x => x.Tags)
			.Where(x => command.ImportListId == null || x.Id == command.ImportListId)
			.Where(x => x.Enable)
			.ToListAsync(cancellationToken);
		if (command.ImportListId is { } requestedId && lists.All(x => x.Id != requestedId))
		{
			throw new KeyNotFoundException($"Import list {requestedId} not found or not enabled");
		}

		var excludedTvdb = await db.ImportListExclusions
			.Where(x => x.TvdbId != null)
			.Select(x => x.TvdbId!.Value)
			.ToListAsync(cancellationToken);
		var excludedTmdb = await db.ImportListExclusions
			.Where(x => x.TmdbId != null)
			.Select(x => x.TmdbId!.Value)
			.ToListAsync(cancellationToken);
		var existingTvdb = await db.Series.Select(x => x.TvdbId).ToListAsync(cancellationToken);
		var existingSeriesTmdb = await db.Series.Where(x => x.TmdbId != null).Select(x => x.TmdbId!.Value).ToListAsync(cancellationToken);
		var existingTmdb = await db.Movies.Select(x => x.TmdbId).ToListAsync(cancellationToken);

		var addedSeries = 0;
		var addedMovies = 0;
		var alreadyInLibrary = 0;
		var excluded = 0;
		var reported = 0;
		var skippedByInterval = 0;
		var failures = new List<string>();

		var seenTvdb = new HashSet<int>();
		var seenSeriesTmdb = new HashSet<int>();
		var seenMovieTmdb = new HashSet<int>();
		var syncedAnySeriesList = false;
		var syncedAnyMovieList = false;
		var skippedAutomaticAddList = false;
		var now = timeProvider.GetUtcNow().UtcDateTime;

		for (var index = 0; index < lists.Count; index++)
		{
			var list = lists[index];
			cancellationToken.ThrowIfCancellationRequested();

			if (isFullSync)
			{
				if (!await statusService.IsAvailableAsync(list.Id, cancellationToken))
				{
					skippedByInterval++;
					skippedAutomaticAddList |= list.EnableAutomaticAdd;
					continue;
				}

				var lastSync = await statusService.GetLastSyncAsync(list.Id, cancellationToken);
				if (lastSync is { } last && now < last + ImportListSchemas.MinRefreshInterval(list.Type))
				{
					skippedByInterval++;
					skippedAutomaticAddList |= list.EnableAutomaticAdd;
					continue;
				}
			}

			try
			{
				var implementation = implementations.FirstOrDefault(x => x.Handles(list.Type))
					?? throw new InvalidOperationException($"No implementation for import list type {list.Type}");
				var items = await implementation.FetchAsync(list, cancellationToken);
				await statusService.RecordSuccessAsync(list.Id, cancellationToken);

				if (list.EnableAutomaticAdd)
				{
					if (list.MediaKind == MediaKind.SERIES)
					{
						syncedAnySeriesList = true;
					}
					else
					{
						syncedAnyMovieList = true;
					}
				}

				foreach (var item in items)
				{
					cancellationToken.ThrowIfCancellationRequested();
					try
					{
						if (list.MediaKind == MediaKind.SERIES)
						{
							var tvdbId = item.TvdbId;
							var tmdbId = item.TmdbId;
							if (tvdbId is null && tmdbId is null)
							{
								continue;
							}

							if (tvdbId is { } seenTvdbId)
							{
								seenTvdb.Add(seenTvdbId);
							}

							if (tmdbId is { } seenSeriesTmdbId)
							{
								seenSeriesTmdb.Add(seenSeriesTmdbId);
							}

							if (tvdbId is { } excludeTvdb && excludedTvdb.Contains(excludeTvdb)
								|| tvdbId is null && tmdbId is { } excludeTmdb && excludedTmdb.Contains(excludeTmdb))
							{
								excluded++;
								continue;
							}

							if (tvdbId is { } haveTvdb && existingTvdb.Contains(haveTvdb)
								|| tvdbId is null && tmdbId is { } haveTmdb && existingSeriesTmdb.Contains(haveTmdb))
							{
								alreadyInLibrary++;
								continue;
							}

							if (!list.EnableAutomaticAdd)
							{
								reported++;
								continue;
							}

							var series = await AddSeriesAsync(list, item, cancellationToken);
							if (tvdbId is { } addedTvdb)
							{
								existingTvdb.Add(addedTvdb);
							}

							if (tmdbId is { } addedSeriesTmdb)
							{
								existingSeriesTmdb.Add(addedSeriesTmdb);
							}

							addedSeries++;
							if (list.SearchOnAdd)
							{
								await commandQueue.EnqueueAsync(new SeriesSearchCommand(series.Id), CommandTrigger.SYSTEM, CommandPriority.LOW, cancellationToken);
							}
						}
						else
						{
							var tmdbId = item.TmdbId;
							if (tmdbId is null)
							{
								continue;
							}

							seenMovieTmdb.Add(tmdbId.Value);

							if (excludedTmdb.Contains(tmdbId.Value))
							{
								excluded++;
								continue;
							}

							if (existingTmdb.Contains(tmdbId.Value))
							{
								alreadyInLibrary++;
								continue;
							}

							if (!list.EnableAutomaticAdd)
							{
								reported++;
								continue;
							}

							var movie = await AddMovieAsync(list, item, cancellationToken);
							existingTmdb.Add(tmdbId.Value);
							addedMovies++;
							if (list.SearchOnAdd)
							{
								await commandQueue.EnqueueAsync(new MovieSearchCommand([movie.Id]), CommandTrigger.SYSTEM, CommandPriority.LOW, cancellationToken);
							}
						}
					}
					catch (Exception exception) when (exception is not OperationCanceledException)
					{
						failures.Add($"'{list.Name}' item '{item.Title}': {exception.Message}");
					}
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				failures.Add($"'{list.Name}': {exception.Message}");
				await statusService.RecordFailureAsync(list.Id, cancellationToken);
			}

			await context.ReportProgressAsync(
				(index + 1) * 100 / Math.Max(lists.Count, 1),
				$"Synced '{list.Name}'",
				cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);

		var cleanLibraryMessage = isFullSync && failures.Count == 0 && !skippedAutomaticAddList
			? await CleanLibraryAsync(syncedAnySeriesList, syncedAnyMovieList, seenTvdb, seenSeriesTmdb, seenMovieTmdb, cancellationToken)
			: null;

		var message = $"Added {addedSeries} series, {addedMovies} movies, {alreadyInLibrary} already present, {excluded} excluded";
		if (reported > 0)
		{
			message += $", {reported} found but automatic add is off";
		}

		if (skippedByInterval > 0)
		{
			message += $", {skippedByInterval} list(s) skipped (minimum refresh interval or backoff)";
		}

		if (cleanLibraryMessage is not null)
		{
			message += $"; {cleanLibraryMessage}";
		}

		if (failures.Count > 0)
		{
			message += $"; {failures.Count} item(s)/list(s) failed: {string.Join("; ", failures)}";
		}

		await context.ReportProgressAsync(100, message, cancellationToken);
	}

	/// <summary>
	///     Applies the configured clean library level to series/movies not covered by any
	///     automatic-add list synced during this run. No-op when the level is disabled or neither
	///     media kind was synced.
	/// </summary>
	private async Task<string?> CleanLibraryAsync(
		bool syncedAnySeriesList,
		bool syncedAnyMovieList,
		IReadOnlySet<int> seenTvdb,
		IReadOnlySet<int> seenSeriesTmdb,
		IReadOnlySet<int> seenMovieTmdb,
		CancellationToken cancellationToken)
	{
		if (!syncedAnySeriesList && !syncedAnyMovieList)
		{
			return null;
		}

		var config = await db.ImportListConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
		var level = config?.CleanLibraryLevel ?? CleanLibraryLevel.DISABLED;
		if (level == CleanLibraryLevel.DISABLED)
		{
			return null;
		}

		var affected = 0;

		if (syncedAnySeriesList)
		{
			var seriesInLibrary = await db.Series.ToListAsync(cancellationToken);
			foreach (var series in seriesInLibrary)
			{
				var covered = seenTvdb.Contains(series.TvdbId) || (series.TmdbId is { } tmdbId && seenSeriesTmdb.Contains(tmdbId));
				if (covered)
				{
					continue;
				}

				affected++;
				await ApplyCleanLibraryAsync(level, isSeries: true, series.Id, cancellationToken);
			}
		}

		if (syncedAnyMovieList)
		{
			var moviesInLibrary = await db.Movies.ToListAsync(cancellationToken);
			foreach (var movie in moviesInLibrary)
			{
				if (seenMovieTmdb.Contains(movie.TmdbId))
				{
					continue;
				}

				affected++;
				await ApplyCleanLibraryAsync(level, isSeries: false, movie.Id, cancellationToken);
			}
		}

		if (affected == 0)
		{
			return null;
		}

		await db.SaveChangesAsync(cancellationToken);
		return $"{affected} item(s) not found on any list ({level})";
	}

	private async Task ApplyCleanLibraryAsync(CleanLibraryLevel level, bool isSeries, int id, CancellationToken cancellationToken)
	{
		switch (level)
		{
			case CleanLibraryLevel.LOG_ONLY:
				break;
			case CleanLibraryLevel.KEEP_AND_UNMONITOR:
				if (isSeries)
				{
					(await db.Series.FirstAsync(x => x.Id == id, cancellationToken)).Monitored = false;
				}
				else
				{
					(await db.Movies.FirstAsync(x => x.Id == id, cancellationToken)).Monitored = false;
				}

				break;
			case CleanLibraryLevel.REMOVE_AND_KEEP:
				if (isSeries)
				{
					await mutator.DeleteSeriesAsync(id, deleteFiles: false, addImportListExclusion: false, cancellationToken);
				}
				else
				{
					await mutator.DeleteMovieAsync(id, deleteFiles: false, addImportListExclusion: false, cancellationToken);
				}

				break;
			case CleanLibraryLevel.REMOVE_AND_DELETE:
				if (isSeries)
				{
					await mutator.DeleteSeriesAsync(id, deleteFiles: true, addImportListExclusion: false, cancellationToken);
				}
				else
				{
					await mutator.DeleteMovieAsync(id, deleteFiles: true, addImportListExclusion: false, cancellationToken);
				}

				break;
			case CleanLibraryLevel.DISABLED:
			default:
				break;
		}
	}

	private async Task<Series> AddSeriesAsync(ImportList list, ImportListItem item, CancellationToken cancellationToken)
		=> await adder.AddSeriesAsync(
			new AddSeriesOptions(
				item.TvdbId,
				item.TmdbId,
				item.TvdbId is null ? MetadataProvider.TMDB : MetadataProvider.TVDB,
				item.Title,
				list.RootFolderId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no root folder"),
				list.SeriesType ?? SeriesType.STANDARD,
				SeriesNumbering.AIRED,
				list.SeasonFolder,
				true,
				AddMonitorOption.ALL,
				false,
				list.Monitor,
				[.. list.Tags.Select(x => x.Id)],
				[
					new VersionOptions(
						"main",
						list.QualityProfileId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no quality profile"),
						list.LanguageProfileId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no language profile"),
						null)
				],
				SearchOnAdd: false),
			cancellationToken);

	private async Task<Movie> AddMovieAsync(ImportList list, ImportListItem item, CancellationToken cancellationToken)
		=> await adder.AddMovieAsync(
			new AddMovieOptions(
				item.TmdbId ?? throw new InvalidOperationException($"Import list '{list.Name}' item has no TMDB id"),
				item.Title,
				list.RootFolderId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no root folder"),
				false,
				true,
				list.MinimumAvailability ?? MinimumAvailability.RELEASED,
				[.. list.Tags.Select(x => x.Id)],
				[
					new VersionOptions(
						"main",
						list.QualityProfileId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no quality profile"),
						list.LanguageProfileId ?? throw new InvalidOperationException($"Import list '{list.Name}' has no language profile"),
						null)
				],
				SearchOnAdd: false),
			cancellationToken);
}
