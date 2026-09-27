using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Library;

/// <summary>Deletes one facade-selected movie version without disturbing native sibling versions.</summary>
public sealed class SelectedMovieDeletionService(SubmarineDbContext db, LibraryMutator mutator, IEventBus eventBus, TimeProvider timeProvider)
{
	public async Task DeleteAsync(int movieId, int mediaVersionId, bool deleteFiles, bool addImportExclusion, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == movieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {movieId} not found.");
		var version = movie.Versions.SingleOrDefault(x => x.Id == mediaVersionId)
			?? throw new InvalidOperationException($"Media version {mediaVersionId} does not belong to movie {movieId}.");
		if (movie.Versions.Count == 1)
		{
			await mutator.DeleteMovieAsync(movieId, deleteFiles, addImportExclusion, cancellationToken);
			return;
		}

		var files = await db.MovieFiles.Where(x => x.MovieId == movieId && x.MediaVersionId == mediaVersionId).ToListAsync(cancellationToken);
		var root = await db.RootFolders.SingleOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
		if (deleteFiles && root is not null)
		{
			try
			{
				var path = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
				if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
			catch (InvalidOperationException) { }
		}

		foreach (var file in files)
		{
			if (root is null) continue;
			var versionFolder = MediaVersionPathGuard.ResolveUnderRoot(root.Path, version.Path);
			var path = Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath));
			await eventBus.PublishAsync(new MovieFileDeletedEvent(movieId, mediaVersionId, path, FileDeleteReason.MANUAL), cancellationToken);
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
	}
}
