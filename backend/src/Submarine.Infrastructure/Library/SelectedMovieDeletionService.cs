using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Library;

/// <summary>Deletes one facade-selected movie version without disturbing native sibling versions.</summary>
public sealed class SelectedMovieDeletionService(
	SubmarineDbContext db,
	LibraryMutator mutator,
	IRecycleBinService recycleBinService,
	IEventBus eventBus,
	TimeProvider timeProvider)
{
	public async Task DeleteAsync(int movieId, int mediaVersionId, bool deleteFiles, bool addImportExclusion, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == movieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {movieId} not found.");
		var version = movie.Versions.SingleOrDefault(x => x.Id == mediaVersionId)
			?? throw new InvalidOperationException($"Media version {mediaVersionId} does not belong to movie {movieId}.");
		if (movie.Versions.Count == 1)
		{
			var movieRoot = deleteFiles
				? await db.RootFolders.SingleOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken)
				: null;
			await mutator.DeleteMovieAsync(movieId, deleteFiles: false, addImportExclusion, cancellationToken);
			if (deleteFiles && movieRoot is not null)
			{
				await RecycleVersionFolderAsync(movieRoot, version.Path, cancellationToken);
			}

			return;
		}

		var files = await db.MovieFiles.Where(x => x.MovieId == movieId && x.MediaVersionId == mediaVersionId).ToListAsync(cancellationToken);
		var root = await db.RootFolders.SingleOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
		var filePaths = new List<string>();
		string? versionFolder = null;
		if (root is not null)
		{
			versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
			filePaths.AddRange(files.Select(file => Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath))));

		}

		foreach (var file in files)
		{
			db.HistoryEvents.Add(new HistoryEvent
			{
				Type = HistoryEventType.DELETED,
				MovieId = movieId,
				MediaVersionId = mediaVersionId,
				SourceTitle = file.SceneName ?? file.RelativePath,
				Quality = file.Quality,
				Languages = file.Languages,
				Date = timeProvider.GetUtcNow().UtcDateTime
			});
		}

		db.MovieFiles.RemoveRange(files);
		db.MediaVersions.Remove(version);
		if (addImportExclusion)
		{
			db.ImportListExclusions.Add(new ImportListExclusion { TmdbId = movie.TmdbId, Title = movie.Title, Year = movie.Year });
		}

		await db.SaveChangesAsync(cancellationToken);

		if (deleteFiles && root is not null)
		{
			await RecycleVersionFolderAsync(root, version.Path, cancellationToken);
		}

		if (root is not null)
		{
			for (var index = 0; index < files.Count; index++)
			{
				await eventBus.PublishAsync(new MovieFileDeletedEvent(movieId, mediaVersionId, filePaths[index], FileDeleteReason.MANUAL), cancellationToken);
			}
		}
	}

	private async Task RecycleVersionFolderAsync(RootFolder root, string versionPath, CancellationToken cancellationToken)
	{
		var versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, versionPath);
		if (!Directory.Exists(versionFolder))
		{
			return;
		}

		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
		foreach (var path in Directory.EnumerateFiles(versionFolder, "*", SearchOption.AllDirectories).ToArray())
		{
			recycleBinService.Recycle(path, root.Path, mediaManagement.RecycleBinPath, timeProvider);
		}

		foreach (var directory in Directory.EnumerateDirectories(versionFolder, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
		{
			if (!Directory.EnumerateFileSystemEntries(directory).Any())
			{
				Directory.Delete(directory);
			}
		}

		if (!Directory.EnumerateFileSystemEntries(versionFolder).Any())
		{
			Directory.Delete(versionFolder);
		}
	}
}
