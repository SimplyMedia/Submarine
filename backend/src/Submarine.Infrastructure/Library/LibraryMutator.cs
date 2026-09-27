using Submarine.Core.Commands;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Core.MediaFiles;

namespace Submarine.Infrastructure.Library;

/// <summary>Editable fields of a series.</summary>
/// <param name="Monitored">New monitored flag, or null to keep.</param>
/// <param name="SeasonFolder">New season folder flag, or null to keep.</param>
/// <param name="SeriesType">New series type, or null to keep.</param>
/// <param name="Numbering">New numbering scheme, or null to keep.</param>
/// <param name="MonitorNewItems">New new item monitoring, or null to keep.</param>
/// <param name="TagIds">Replaces tags, or null to keep.</param>
/// <param name="RootFolderId">Moves all versions to this root folder, or null to keep.</param>
/// <param name="MoveFiles">Whether files move on disk when the root folder changes.</param>
/// <param name="Versions">Per version edits.</param>
public sealed record UpdateSeriesOptions(
	bool? Monitored,
	bool? SeasonFolder,
	SeriesType? SeriesType,
	SeriesNumbering? Numbering,
	MonitorNewItems? MonitorNewItems,
	IReadOnlyList<int>? TagIds,
	int? RootFolderId,
	bool MoveFiles,
	IReadOnlyList<UpdateVersionOptions> Versions);

/// <summary>Editable fields of a version.</summary>
/// <param name="Id">Version id.</param>
/// <param name="Name">New display name, or null to keep.</param>
/// <param name="QualityProfileId">New quality profile, or null to keep.</param>
/// <param name="LanguageProfileId">New language profile, or null to keep.</param>
/// <param name="Path">New folder name inside the root folder, or null to keep.</param>
public sealed record UpdateVersionOptions(int Id, string? Name, int? QualityProfileId, int? LanguageProfileId, string? Path);

/// <summary>Editable fields of a movie.</summary>
/// <param name="Monitored">New monitored flag, or null to keep.</param>
/// <param name="MinimumAvailability">New minimum availability, or null to keep.</param>
/// <param name="IsAnime">New anime flag, or null to keep.</param>
/// <param name="TagIds">Replaces tags, or null to keep.</param>
/// <param name="RootFolderId">Moves all versions to this root folder, or null to keep.</param>
/// <param name="MoveFiles">Whether files move on disk when the root folder changes.</param>
/// <param name="Versions">Per version edits.</param>
public sealed record UpdateMovieOptions(
	bool? Monitored,
	MinimumAvailability? MinimumAvailability,
	bool? IsAnime,
	IReadOnlyList<int>? TagIds,
	int? RootFolderId,
	bool MoveFiles,
	IReadOnlyList<UpdateVersionOptions> Versions);

/// <summary>Result of an update that may move a series or movie.</summary>
/// <param name="Moved">Whether a move command was queued instead of changing rows directly.</param>
public sealed record UpdateResult(bool Moved);

/// <summary>Bulk update of series.</summary>
/// <param name="Ids">Series ids.</param>
/// <param name="Monitored">New monitored flag, or null to keep.</param>
/// <param name="SeriesType">New series type, or null to keep.</param>
/// <param name="SeasonFolder">New season folder flag, or null to keep.</param>
/// <param name="MonitorNewItems">New new item monitoring, or null to keep.</param>
/// <param name="QualityProfileId">Applied to all versions, or null to keep.</param>
/// <param name="LanguageProfileId">Applied to all versions, or null to keep.</param>
/// <param name="RootFolderId">Moves all versions to this root folder, or null to keep.</param>
/// <param name="MoveFiles">Whether files move on disk when the root folder changes.</param>
/// <param name="TagOperation">Tag change, or null to keep.</param>
public sealed record BulkUpdateSeriesOptions(
	IReadOnlyList<int> Ids,
	bool? Monitored,
	SeriesType? SeriesType,
	bool? SeasonFolder,
	MonitorNewItems? MonitorNewItems,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	bool MoveFiles,
	TagOperation? TagOperation);

/// <summary>Bulk update of movies.</summary>
/// <param name="Ids">Movie ids.</param>
/// <param name="Monitored">New monitored flag, or null to keep.</param>
/// <param name="MinimumAvailability">New minimum availability, or null to keep.</param>
/// <param name="QualityProfileId">Applied to all versions, or null to keep.</param>
/// <param name="LanguageProfileId">Applied to all versions, or null to keep.</param>
/// <param name="RootFolderId">Moves all versions to this root folder, or null to keep.</param>
/// <param name="MoveFiles">Whether files move on disk when the root folder changes.</param>
/// <param name="TagOperation">Tag change, or null to keep.</param>
public sealed record BulkUpdateMovieOptions(
	IReadOnlyList<int> Ids,
	bool? Monitored,
	MinimumAvailability? MinimumAvailability,
	int? QualityProfileId,
	int? LanguageProfileId,
	int? RootFolderId,
	bool MoveFiles,
	TagOperation? TagOperation);

/// <summary>Tag change applied in bulk.</summary>
/// <param name="Mode">Add, remove or replace.</param>
/// <param name="TagIds">Tag ids the mode applies to.</param>
public sealed record TagOperation(TagOperationMode Mode, IReadOnlyList<int> TagIds);

/// <summary>How a tag operation changes tags.</summary>
public enum TagOperationMode
{
	/// <summary>Add tags to existing ones.</summary>
	ADD,

	/// <summary>Remove tags.</summary>
	REMOVE,

	/// <summary>Replace all tags.</summary>
	REPLACE
}

/// <summary>Result of a bulk delete.</summary>
/// <param name="Deleted">Number of items deleted.</param>
public sealed record BulkDeleteResult(int Deleted);

/// <summary>
///     Updates and deletes series and movies, including version moves and folder deletion.
/// </summary>
public sealed class LibraryMutator(
	SubmarineDbContext db,
	ICommandQueue commandQueue,
	IEventBus eventBus,
	TimeProvider timeProvider)
{
	/// <summary>
	///     Update editable fields of a series. A root folder change with MoveFiles queues
	///     a <see cref="MoveSeriesCommand" /> that moves folders on disk; without it only rows change.
	/// </summary>
	public async Task<UpdateResult> UpdateSeriesAsync(int id, UpdateSeriesOptions options, CancellationToken cancellationToken = default)
	{
		var series = await db.Series
			.Include(x => x.Versions)
			.Include(x => x.Tags)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Series {id} not found");

		if (options.Monitored is { } monitored)
		{
			series.Monitored = monitored;
		}

		if (options.SeasonFolder is { } seasonFolder)
		{
			series.SeasonFolder = seasonFolder;
		}

		if (options.SeriesType is { } seriesType)
		{
			series.Type = seriesType;
		}

		if (options.Numbering is { } numbering)
		{
			series.Numbering = numbering;
		}

		if (options.MonitorNewItems is { } monitorNewItems)
		{
			series.MonitorNewItems = monitorNewItems;
		}

		if (options.TagIds is { } tagIds)
		{
			series.Tags = await LoadTagsAsync(tagIds, cancellationToken);
		}

		await ApplyVersionEditsAsync(options.Versions, seriesId: id, movieId: null, cancellationToken);

		if (options.RootFolderId is { } rootFolderId)
		{
			await RequireRootFolderAsync(rootFolderId, MediaKind.SERIES, cancellationToken);
		}

		var moved = options.RootFolderId is { } moveTarget
			&& series.Versions.Any(x => x.RootFolderId != moveTarget)
			&& options.MoveFiles;

		if (!moved && options.RootFolderId is { } changeTarget)
		{
			foreach (var version in series.Versions)
			{
				version.RootFolderId = changeTarget;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		if (moved)
		{
			await commandQueue.EnqueueAsync(new MoveSeriesCommand(id, options.RootFolderId!.Value, true), CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
		}

		await eventBus.PublishAsync(new SeriesUpdatedEvent(id), cancellationToken);
		return new UpdateResult(moved);
	}

	/// <summary>
	///     Update editable fields of a movie.
	/// </summary>
	public async Task<UpdateResult> UpdateMovieAsync(int id, UpdateMovieOptions options, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies
			.Include(x => x.Versions)
			.Include(x => x.Tags)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {id} not found");

		if (options.Monitored is { } monitored)
		{
			movie.Monitored = monitored;
		}

		if (options.MinimumAvailability is { } minimumAvailability)
		{
			movie.MinimumAvailability = minimumAvailability;
		}

		if (options.IsAnime is { } isAnime)
		{
			movie.IsAnime = isAnime;
		}

		if (options.TagIds is { } tagIds)
		{
			movie.Tags = await LoadTagsAsync(tagIds, cancellationToken);
		}

		await ApplyVersionEditsAsync(options.Versions, seriesId: null, movieId: id, cancellationToken);

		if (options.RootFolderId is { } rootFolderId)
		{
			await RequireRootFolderAsync(rootFolderId, MediaKind.MOVIES, cancellationToken);
		}

		var moved = options.RootFolderId is { } moveTarget
			&& movie.Versions.Any(x => x.RootFolderId != moveTarget)
			&& options.MoveFiles;

		if (!moved && options.RootFolderId is { } changeTarget)
		{
			foreach (var version in movie.Versions)
			{
				version.RootFolderId = changeTarget;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		if (moved)
		{
			await commandQueue.EnqueueAsync(new MoveMovieCommand(id, options.RootFolderId!.Value, true), CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
		}

		await eventBus.PublishAsync(new MovieUpdatedEvent(id), cancellationToken);
		return new UpdateResult(moved);
	}

	/// <summary>
	///     Bulk update series.
	/// </summary>
	public async Task<int> BulkUpdateSeriesAsync(BulkUpdateSeriesOptions options, CancellationToken cancellationToken = default)
	{
		if (options.RootFolderId is not null)
		{
			await RequireRootFolderAsync(options.RootFolderId.Value, MediaKind.SERIES, cancellationToken);
		}

		var tags = options.TagOperation is { } tagOperation
			? await LoadTagsAsync(tagOperation.TagIds, cancellationToken)
			: [];

		var seriesList = await db.Series
			.Include(x => x.Versions)
			.Include(x => x.Tags)
			.Where(x => options.Ids.Contains(x.Id))
			.ToListAsync(cancellationToken);

		foreach (var series in seriesList)
		{
			if (options.Monitored is { } monitored)
			{
				series.Monitored = monitored;
			}

			if (options.SeriesType is { } seriesType)
			{
				series.Type = seriesType;
			}

			if (options.SeasonFolder is { } seasonFolder)
			{
				series.SeasonFolder = seasonFolder;
			}

			if (options.MonitorNewItems is { } monitorNewItems)
			{
				series.MonitorNewItems = monitorNewItems;
			}

			foreach (var version in series.Versions)
			{
				if (options.QualityProfileId is { } qualityProfileId)
				{
					version.QualityProfileId = qualityProfileId;
				}

				if (options.LanguageProfileId is { } languageProfileId)
				{
					version.LanguageProfileId = languageProfileId;
				}
			}

			ApplyTagOperation(series.Tags, options.TagOperation, tags);

			if (options.RootFolderId is { } rootId && series.Versions.All(x => x.RootFolderId != rootId))
			{
				if (options.MoveFiles)
				{
					await commandQueue.EnqueueAsync(new MoveSeriesCommand(series.Id, rootId, true), CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
				}
				else
				{
					foreach (var version in series.Versions)
					{
						version.RootFolderId = rootId;
					}
				}
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		return seriesList.Count;
	}

	/// <summary>
	///     Bulk update movies.
	/// </summary>
	public async Task<int> BulkUpdateMoviesAsync(BulkUpdateMovieOptions options, CancellationToken cancellationToken = default)
	{
		if (options.RootFolderId is not null)
		{
			await RequireRootFolderAsync(options.RootFolderId.Value, MediaKind.MOVIES, cancellationToken);
		}

		var tags = options.TagOperation is { } tagOperation
			? await LoadTagsAsync(tagOperation.TagIds, cancellationToken)
			: [];

		var movies = await db.Movies
			.Include(x => x.Versions)
			.Include(x => x.Tags)
			.Where(x => options.Ids.Contains(x.Id))
			.ToListAsync(cancellationToken);

		foreach (var movie in movies)
		{
			if (options.Monitored is { } monitored)
			{
				movie.Monitored = monitored;
			}

			if (options.MinimumAvailability is { } minimumAvailability)
			{
				movie.MinimumAvailability = minimumAvailability;
			}

			foreach (var version in movie.Versions)
			{
				if (options.QualityProfileId is { } qualityProfileId)
				{
					version.QualityProfileId = qualityProfileId;
				}

				if (options.LanguageProfileId is { } languageProfileId)
				{
					version.LanguageProfileId = languageProfileId;
				}
			}

			ApplyTagOperation(movie.Tags, options.TagOperation, tags);

			if (options.RootFolderId is { } rootId && movie.Versions.All(x => x.RootFolderId != rootId))
			{
				if (options.MoveFiles)
				{
					await commandQueue.EnqueueAsync(new MoveMovieCommand(movie.Id, rootId, true), CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
				}
				else
				{
					foreach (var version in movie.Versions)
					{
						version.RootFolderId = rootId;
					}
				}
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		return movies.Count;
	}

	/// <summary>
	///     Delete a series, optionally its files on disk, optionally adding an import list exclusion.
	/// </summary>
	public async Task DeleteSeriesAsync(int id, bool deleteFiles, bool addImportListExclusion, CancellationToken cancellationToken = default)
	{
		var series = await db.Series
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Series {id} not found");

		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		if (deleteFiles)
		{
			DeleteFolders(series.Versions, rootPaths);
		}

		if (addImportListExclusion)
		{
			db.ImportListExclusions.Add(new ImportListExclusion
			{
				TvdbId = series.TvdbId,
				Title = series.Title,
				Year = series.Year
			});
		}

		db.Series.Remove(series);
		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new SeriesDeletedEvent(id, series.Title, deleteFiles, series.Year ?? 0), cancellationToken);
	}

	/// <summary>
	///     Delete a movie, optionally its files on disk, optionally adding an import list exclusion.
	/// </summary>
	public async Task DeleteMovieAsync(int id, bool deleteFiles, bool addImportListExclusion, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies
			.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {id} not found");

		var rootPaths = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);
		if (deleteFiles)
		{
			DeleteFolders(movie.Versions, rootPaths);
		}

		if (addImportListExclusion)
		{
			db.ImportListExclusions.Add(new ImportListExclusion
			{
				TmdbId = movie.TmdbId,
				Title = movie.Title,
				Year = movie.Year
			});
		}

		db.Movies.Remove(movie);
		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new MovieDeletedEvent(id, movie.Title, deleteFiles, movie.TmdbId, movie.Year), cancellationToken);
	}

	/// <summary>
	///     Bulk delete series.
	/// </summary>
	public async Task<BulkDeleteResult> BulkDeleteSeriesAsync(IReadOnlyList<int> ids, bool deleteFiles, bool addImportListExclusion, CancellationToken cancellationToken = default)
	{
		foreach (var id in ids)
		{
			await DeleteSeriesAsync(id, deleteFiles, addImportListExclusion, cancellationToken);
		}

		return new BulkDeleteResult(ids.Count);
	}

	/// <summary>
	///     Bulk delete movies.
	/// </summary>
	public async Task<BulkDeleteResult> BulkDeleteMoviesAsync(IReadOnlyList<int> ids, bool deleteFiles, bool addImportListExclusion, CancellationToken cancellationToken = default)
	{
		foreach (var id in ids)
		{
			await DeleteMovieAsync(id, deleteFiles, addImportListExclusion, cancellationToken);
		}

		return new BulkDeleteResult(ids.Count);
	}

	/// <summary>
	///     Set the monitored flag of one season and its episodes.
	/// </summary>
	public async Task SetSeasonMonitoredAsync(int seriesId, int seasonNumber, bool monitored, CancellationToken cancellationToken = default)
	{
		var season = await db.Seasons
			.FirstOrDefaultAsync(x => x.SeriesId == seriesId && x.SeasonNumber == seasonNumber, cancellationToken)
			?? throw new KeyNotFoundException($"Season {seasonNumber} of series {seriesId} not found");

		season.Monitored = monitored;
		await db.Episodes
			.Where(x => x.SeriesId == seriesId && x.SeasonNumber == seasonNumber)
			.ExecuteUpdateAsync(set => set.SetProperty(x => x.Monitored, monitored), cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new SeriesUpdatedEvent(seriesId), cancellationToken);
	}

	/// <summary>
	///     Apply a season pass: an explicit monitored map per season, or a monitor option.
	/// </summary>
	public async Task ApplySeasonPassAsync(int seriesId, IReadOnlyDictionary<int, bool> seasonMap, AddMonitorOption? option, bool monitorSpecials, CancellationToken cancellationToken = default)
	{
		var series = await db.Series
			.Include(x => x.Seasons)
			.Include(x => x.Episodes)
			.FirstOrDefaultAsync(x => x.Id == seriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {seriesId} not found");

		if (option is { } monitorOption)
		{
			MonitorRules.Apply(series, monitorOption, monitorSpecials, timeProvider.GetUtcNow().UtcDateTime);
		}

		foreach (var season in series.Seasons)
		{
			if (seasonMap.TryGetValue(season.SeasonNumber, out var monitored))
			{
				season.Monitored = monitored;
			}
		}

		foreach (var episode in series.Episodes)
		{
			if (seasonMap.TryGetValue(episode.SeasonNumber, out var monitored))
			{
				episode.Monitored = monitored;
			}
		}

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new SeriesUpdatedEvent(seriesId), cancellationToken);
	}

	/// <summary>
	///     Apply an episode monitored change.
	/// </summary>
	public async Task SetEpisodeMonitoredAsync(int episodeId, bool monitored, CancellationToken cancellationToken = default)
	{
		var affected = await db.Episodes
			.Where(x => x.Id == episodeId)
			.ExecuteUpdateAsync(set => set.SetProperty(x => x.Monitored, monitored), cancellationToken);
		if (affected == 0)
		{
			throw new KeyNotFoundException($"Episode {episodeId} not found");
		}

		await eventBus.PublishAsync(new EpisodeUpdatedEvent(episodeId), cancellationToken);
	}

	/// <summary>
	///     Set the monitored flag of many episodes at once.
	/// </summary>
	public async Task<int> SetEpisodesMonitoredAsync(IReadOnlyList<int> episodeIds, bool monitored, CancellationToken cancellationToken = default)
	{
		var affected = await db.Episodes
			.Where(x => episodeIds.Contains(x.Id))
			.ExecuteUpdateAsync(set => set.SetProperty(x => x.Monitored, monitored), cancellationToken);
		foreach (var episodeId in episodeIds)
		{
			await eventBus.PublishAsync(new EpisodeUpdatedEvent(episodeId), cancellationToken);
		}

		return affected;
	}

	private async Task ApplyVersionEditsAsync(IReadOnlyList<UpdateVersionOptions> edits, int? seriesId, int? movieId, CancellationToken cancellationToken)
	{
		foreach (var edit in edits)
		{
			var version = await db.MediaVersions
				.FirstOrDefaultAsync(x => x.Id == edit.Id && (seriesId == null || x.SeriesId == seriesId) && (movieId == null || x.MovieId == movieId), cancellationToken)
				?? throw new KeyNotFoundException($"Version {edit.Id} not found");

			if (edit.Name is { } name)
			{
				version.Name = name;
			}

			if (edit.QualityProfileId is { } qualityProfileId)
			{
				_ = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == qualityProfileId, cancellationToken)
					?? throw new KeyNotFoundException($"Quality profile {qualityProfileId} not found");
				version.QualityProfileId = qualityProfileId;
			}

			if (edit.LanguageProfileId is { } languageProfileId)
			{
				_ = await db.LanguageProfiles.FirstOrDefaultAsync(x => x.Id == languageProfileId, cancellationToken)
					?? throw new KeyNotFoundException($"Language profile {languageProfileId} not found");
				version.LanguageProfileId = languageProfileId;
			}

			if (edit.Path is { } path)
			{
				if (!MediaVersionPathGuard.IsSingleRelativeSegment(path))
				{
					throw new ValidationException("Version path must be a single relative folder segment (no separators, not '.' or '..')");
				}

				var existingPaths = await db.MediaVersions
					.Where(x => x.RootFolderId == version.RootFolderId && x.Id != version.Id)
					.Select(x => x.Path)
					.ToListAsync(cancellationToken);
				if (existingPaths.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
					|| db.MediaVersions.Local.Any(existing => existing.Id != version.Id
						&& existing.RootFolderId == version.RootFolderId
						&& string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase)))
				{
					throw new ValidationException($"Version path '{path}' is already used in this root folder");
				}

				version.Path = path;
			}
		}
	}

	private void ApplyTagOperation(ICollection<Tag> current, TagOperation? operation, List<Tag> tags)
	{
		if (operation is not { } op)
		{
			return;
		}

		switch (op.Mode)
		{
			case TagOperationMode.ADD:
				foreach (var tag in tags.Where(tag => current.All(x => x.Id != tag.Id)))
				{
					current.Add(tag);
				}

				break;
			case TagOperationMode.REMOVE:
				foreach (var tag in current.Where(tag => tags.Any(x => x.Id == tag.Id)).ToList())
				{
					current.Remove(tag);
				}

				break;
			case TagOperationMode.REPLACE:
				current.Clear();
				foreach (var tag in tags)
				{
					current.Add(tag);
				}

				break;
		}
	}

	private async Task<RootFolder> RequireRootFolderAsync(int rootFolderId, MediaKind kind, CancellationToken cancellationToken)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");
		if (root.MediaKind != kind)
		{
			throw new ValidationException($"Root folder '{root.Path}' does not hold {kind.ToString().ToLowerInvariant()}");
		}

		return root;
	}

	private async Task<List<Tag>> LoadTagsAsync(IReadOnlyList<int> tagIds, CancellationToken cancellationToken)
	{
		if (tagIds.Count == 0)
		{
			return [];
		}

		var found = await db.Tags.Where(x => tagIds.Contains(x.Id)).ToListAsync(cancellationToken);
		if (found.Count != tagIds.Distinct().Count())
		{
			throw new KeyNotFoundException("One or more tags not found");
		}

		return found;
	}

	private static void DeleteFolders(IEnumerable<MediaVersion> versions, IReadOnlyDictionary<int, string> rootPaths)
	{
		foreach (var version in versions)
		{
			if (!rootPaths.TryGetValue(version.RootFolderId, out var rootPath))
			{
				continue;
			}

			try
			{
				var fullPath = MediaVersionPathGuard.ResolveUnderRoot(rootPath, version.Path);
				if (Directory.Exists(fullPath))
				{
					Directory.Delete(fullPath, true);
				}
			}
			catch (IOException)
			{
				// A missing or locked folder must not block removing the library entry.
			}
			catch (UnauthorizedAccessException)
			{
				// A missing or locked folder must not block removing the library entry.
			}
			catch (InvalidOperationException)
			{
				// An invalid version path must never touch anything outside its root folder.
			}
		}
	}
}
