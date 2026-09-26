using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <summary>Native movie-file deletion shared by the native API and compatibility facades.</summary>
public sealed class MovieFileOperationService(
	SubmarineDbContext db,
	IRecycleBinService recycleBin,
	IEventBus eventBus,
	TimeProvider timeProvider)
{
	public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
	{
		var file = await db.MovieFiles.Include(x => x.MediaVersion).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (file is null) return false;
		await DeleteOneAsync(file, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<int> DeleteManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Distinct().Count() != ids.Count) throw new ArgumentException("Movie file ids must be unique.", nameof(ids));
		var files = await db.MovieFiles.Include(x => x.MediaVersion).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
		if (files.Count != ids.Count) throw new KeyNotFoundException("One or more movie files were not found.");
		foreach (var file in files) await DeleteOneAsync(file, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return files.Count;
	}

	private async Task DeleteOneAsync(MovieFile file, CancellationToken cancellationToken)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, cancellationToken);
		if (root is not null)
		{
			var settings = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
			var fullPath = Path.GetFullPath(Path.Combine(root.Path, file.MediaVersion.Path, file.RelativePath));
			recycleBin.Recycle(fullPath, root.Path, settings.RecycleBinPath, timeProvider);
			await eventBus.PublishAsync(new MovieFileDeletedEvent(file.MovieId, file.MediaVersionId, fullPath, FileDeleteReason.MANUAL), cancellationToken);
		}
		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.DELETED,
			MovieId = file.MovieId,
			MediaVersionId = file.MediaVersionId,
			SourceTitle = file.SceneName ?? file.RelativePath,
			Quality = file.Quality,
			Languages = file.Languages,
			Date = timeProvider.GetUtcNow().UtcDateTime
		});
		db.MovieFiles.Remove(file);
	}
}
