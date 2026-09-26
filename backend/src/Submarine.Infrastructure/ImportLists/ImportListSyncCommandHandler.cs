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
/// </summary>
public sealed class ImportListSyncCommandHandler(
	SubmarineDbContext db,
	IEnumerable<IImportList> implementations,
	LibraryAdder adder,
	ICommandQueue commandQueue)
	: ICommandHandler<ImportListSyncCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(ImportListSyncCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
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
		var failures = new List<string>();

		for (var index = 0; index < lists.Count; index++)
		{
			var list = lists[index];
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				var implementation = implementations.FirstOrDefault(x => x.Handles(list.Type))
					?? throw new InvalidOperationException($"No implementation for import list type {list.Type}");
				var items = await implementation.FetchAsync(list, cancellationToken);
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
			}

			await context.ReportProgressAsync(
				(index + 1) * 100 / Math.Max(lists.Count, 1),
				$"Synced '{list.Name}'",
				cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		var message = $"Added {addedSeries} series, {addedMovies} movies, {alreadyInLibrary} already present, {excluded} excluded";
		if (reported > 0)
		{
			message += $", {reported} found but automatic add is off";
		}

		if (failures.Count > 0)
		{
			message += $"; {failures.Count} item(s)/list(s) failed: {string.Join("; ", failures)}";
		}

		await context.ReportProgressAsync(100, message, cancellationToken);
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

