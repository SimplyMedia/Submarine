using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.RootFolders;

public sealed class RootFolderService(SubmarineDbContext db)
{
	public Task<RootFolder?> FindAsync(int id, CancellationToken cancellationToken)
		=> db.RootFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
	public async Task<RootFolderMutation> CreateAsync(string requestedPath, MediaKind mediaKind, CancellationToken cancellationToken)
	{
		if (!TryNormalize(requestedPath, out var path) || !Directory.Exists(path))
		{
			return RootFolderMutation.InvalidPath(requestedPath);
		}

		var existing = await db.RootFolders.FirstOrDefaultAsync(x => x.Path == path, cancellationToken);
		if (existing is not null)
		{
			return existing.MediaKind == mediaKind
				? RootFolderMutation.Conflict($"Root folder '{path}' already exists")
				: RootFolderMutation.IncompatiblePath($"Path '{path}' is already a root folder for {existing.MediaKind}");
		}

		var folder = new RootFolder { Path = path, MediaKind = mediaKind };
		db.RootFolders.Add(folder);
		await db.SaveChangesAsync(cancellationToken);
		return RootFolderMutation.Success(folder);
	}

	public async Task<RootFolderMutation> UpdateAsync(int id, string requestedPath, MediaKind mediaKind, CancellationToken cancellationToken)
	{
		var folder = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (folder is null)
		{
			return RootFolderMutation.NotFound($"Root folder {id} not found");
		}

		if (!TryNormalize(requestedPath, out var path) || !Directory.Exists(path))
		{
			return RootFolderMutation.InvalidPath(requestedPath);
		}

		if (await db.RootFolders.AnyAsync(x => x.Path == path && x.Id != id, cancellationToken))
		{
			return RootFolderMutation.Conflict($"Root folder '{path}' already exists");
		}

		folder.Path = path;
		folder.MediaKind = mediaKind;
		await db.SaveChangesAsync(cancellationToken);
		return RootFolderMutation.Success(folder);
	}

	public async Task<RootFolderMutation> DeleteAsync(int id, CancellationToken cancellationToken)
	{
		var folder = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (folder is null)
		{
			return RootFolderMutation.NotFound($"Root folder {id} not found");
		}

		var inUse = await db.MediaVersions.AnyAsync(x => x.RootFolderId == id, cancellationToken)
			|| await db.ImportLists.AnyAsync(x => x.RootFolderId == id, cancellationToken)
			|| await db.Collections.AnyAsync(x => x.RootFolderId == id, cancellationToken);
		if (inUse)
		{
			return RootFolderMutation.Conflict($"Root folder '{folder.Path}' is still in use");
		}

		db.RootFolders.Remove(folder);
		await db.SaveChangesAsync(cancellationToken);
		return RootFolderMutation.Success(folder);
	}
	private static bool TryNormalize(string path, out string fullPath)
	{
		try
		{
			fullPath = Path.GetFullPath(path);
			return true;
		}
		catch (Exception exception) when (exception is ArgumentException or PathTooLongException)
		{
			fullPath = "";
			return false;
		}
	}
}

public sealed record RootFolderMutation(RootFolder? Folder, int StatusCode, string? Error)
{
	public static RootFolderMutation Success(RootFolder folder) => new(folder, StatusCodes.Status200OK, null);
	public static RootFolderMutation InvalidPath(string path) => new(null, StatusCodes.Status400BadRequest, $"Path '{path}' does not exist");
	public static RootFolderMutation IncompatiblePath(string error) => new(null, StatusCodes.Status400BadRequest, error);
	public static RootFolderMutation Conflict(string error) => new(null, StatusCodes.Status409Conflict, error);
	public static RootFolderMutation NotFound(string error) => new(null, StatusCodes.Status404NotFound, error);
}
