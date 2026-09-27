using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.MediaVersions;

/// <summary>
///     Changes the root folder of one media version without touching sibling versions of the same title.
///     Used by the native <c>PUT /api/v1/media-versions/{id}</c> endpoint and by the Radarr/Sonarr compatibility
///     facades, which may only mutate the facade-selected version rather than every version of a title.
/// </summary>
public sealed class MediaVersionMover(SubmarineDbContext db, ICommandQueue commandQueue)
{
	/// <summary>
	///     Changes <paramref name="mediaVersionId" />'s root folder to <paramref name="rootFolderId" />.
	///     When <paramref name="moveFiles" /> is true and the root actually changes, the physical folder move
	///     happens asynchronously through a queued <see cref="MoveMediaVersionCommand" /> and the version row is
	///     only updated once that command completes. Otherwise the row is updated immediately.
	/// </summary>
	/// <returns>True when a move was queued; false when the row was updated immediately or nothing changed.</returns>
	public async Task<bool> ChangeRootFolderAsync(int mediaVersionId, int rootFolderId, bool moveFiles, CancellationToken cancellationToken = default)
	{
		var version = await db.MediaVersions.FirstOrDefaultAsync(x => x.Id == mediaVersionId, cancellationToken)
			?? throw new KeyNotFoundException($"Media version {mediaVersionId} not found");
		var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == rootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {rootFolderId} not found");
		var kind = version.SeriesId is null ? MediaKind.MOVIES : MediaKind.SERIES;
		if (root.MediaKind != kind)
		{
			throw new FluentValidation.ValidationException($"Root folder '{root.Path}' does not match the version");
		}

		if (version.RootFolderId == rootFolderId)
		{
			return false;
		}

		if (moveFiles)
		{
			await commandQueue.EnqueueAsync(new MoveMediaVersionCommand(mediaVersionId, rootFolderId), CommandTrigger.MANUAL, CommandPriority.NORMAL, cancellationToken);
			return true;
		}

		version.RootFolderId = rootFolderId;
		await db.SaveChangesAsync(cancellationToken);
		return false;
	}
}
