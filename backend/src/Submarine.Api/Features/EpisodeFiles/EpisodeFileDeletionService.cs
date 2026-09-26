using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.EpisodeFiles;

/// <summary>
///     Deletes episode files: recycles or removes the file on disk, records history and publishes the deletion
///     event. Shared by the native episode-files endpoints and the Sonarr compatibility facade so both delete
///     through one path.
/// </summary>
public sealed class EpisodeFileDeletionService(
	SubmarineDbContext db,
	IRecycleBinService recycleBinService,
	IEventBus eventBus,
	TimeProvider timeProvider)
{
	/// <summary>Deletes one episode file by id. Returns false when the file does not exist.</summary>
	public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
	{
		var file = await db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (file is null)
		{
			return false;
		}

		await DeleteOneAsync(file, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);
		return true;
	}

	/// <summary>Deletes many episode files by id. Ids that do not exist are ignored. Returns the number deleted.</summary>
	public async Task<int> DeleteManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
	{
		var files = await db.EpisodeFiles.Include(x => x.Episodes).Include(x => x.MediaVersion)
			.Where(x => ids.Contains(x.Id))
			.ToListAsync(cancellationToken);
		foreach (var file in files)
		{
			await DeleteOneAsync(file, cancellationToken);
		}

		await db.SaveChangesAsync(cancellationToken);
		return files.Count;
	}

	private async Task DeleteOneAsync(EpisodeFile file, CancellationToken cancellationToken)
	{
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == file.MediaVersion.RootFolderId, cancellationToken);
		if (root is not null)
		{
			var mediaManagement = await db.MediaManagementConfig.AsNoTracking().SingleAsync(cancellationToken);
			var fullPath = Path.GetFullPath(Path.Combine(root.Path, file.MediaVersion.Path, file.RelativePath));
			recycleBinService.Recycle(fullPath, root.Path, mediaManagement.RecycleBinPath, timeProvider);
			await eventBus.PublishAsync(
				new EpisodeFileDeletedEvent(file.SeriesId, file.MediaVersionId, [.. file.Episodes.Select(x => x.Id)], fullPath, FileDeleteReason.MANUAL),
				cancellationToken);
		}

		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.DELETED,
			SeriesId = file.SeriesId,
			EpisodeId = file.Episodes.FirstOrDefault()?.Id,
			MediaVersionId = file.MediaVersionId,
			SourceTitle = file.SceneName ?? file.RelativePath,
			Quality = file.Quality,
			Languages = file.Languages,
			Date = timeProvider.GetUtcNow().UtcDateTime
		});

		db.EpisodeFiles.Remove(file);
	}
}
