using Submarine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Common;
using Submarine.Core.Events;
using Submarine.Core.Library;
using Submarine.Core.MediaFiles;
using Submarine.Infrastructure.Commands;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Moves series version folders to a new root folder on disk and updates the rows.
/// </summary>
public sealed class MoveSeriesCommandHandler(SubmarineDbContext db, IEventBus eventBus)
	: ICommandHandler<MoveSeriesCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(MoveSeriesCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var series = await db.Series.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == command.SeriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {command.SeriesId} not found");
		var targetRoot = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == command.RootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {command.RootFolderId} not found");
		var oldRoots = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);

		var renames = new List<RenamedFile>();
		foreach (var version in series.Versions)
		{
			if (version.RootFolderId == targetRoot.Id)
			{
				continue;
			}

			if (command.MoveFiles
				&& oldRoots.TryGetValue(version.RootFolderId, out var oldRootPath))
			{
				var source = MediaVersionPathGuard.ResolveUnderRoot(oldRootPath, version.Path);
				var destination = MediaVersionPathGuard.ResolveUnderRoot(targetRoot.Path, version.Path);
				if (Directory.Exists(source) && !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(targetRoot.Path);
					if (Directory.Exists(destination))
					{
						throw new ConflictException($"Destination folder '{destination}' already exists");
					}

					VersionFolderMove.MoveWithFallback(source, destination);
					renames.Add(new RenamedFile(source, destination));
				}
			}

			version.RootFolderId = targetRoot.Id;
			await db.SaveChangesAsync(cancellationToken);
		}

		if (renames.Count > 0)
		{
			await eventBus.PublishAsync(new MediaRenamedEvent(series.Id, null, renames), cancellationToken);
		}

		await context.ReportProgressAsync(100, null, cancellationToken);
	}
}

/// <summary>
///     Moves movie version folders to a new root folder on disk and updates the rows.
/// </summary>
public sealed class MoveMovieCommandHandler(SubmarineDbContext db, IEventBus eventBus)
	: ICommandHandler<MoveMovieCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(MoveMovieCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions)
			.FirstOrDefaultAsync(x => x.Id == command.MovieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {command.MovieId} not found");
		var targetRoot = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == command.RootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {command.RootFolderId} not found");
		var oldRoots = await db.RootFolders.ToDictionaryAsync(x => x.Id, x => x.Path, cancellationToken);

		var renames = new List<RenamedFile>();
		foreach (var version in movie.Versions)
		{
			if (version.RootFolderId == targetRoot.Id)
			{
				continue;
			}

			if (command.MoveFiles
				&& oldRoots.TryGetValue(version.RootFolderId, out var oldRootPath))
			{
				var source = MediaVersionPathGuard.ResolveUnderRoot(oldRootPath, version.Path);
				var destination = MediaVersionPathGuard.ResolveUnderRoot(targetRoot.Path, version.Path);
				if (Directory.Exists(source) && !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(targetRoot.Path);
					if (Directory.Exists(destination))
					{
						throw new ConflictException($"Destination folder '{destination}' already exists");
					}

					VersionFolderMove.MoveWithFallback(source, destination);
					renames.Add(new RenamedFile(source, destination));
				}
			}

			version.RootFolderId = targetRoot.Id;
			await db.SaveChangesAsync(cancellationToken);
		}

		if (renames.Count > 0)
		{
			await eventBus.PublishAsync(new MediaRenamedEvent(null, movie.Id, renames), cancellationToken);
		}

		await context.ReportProgressAsync(100, null, cancellationToken);
	}
}

/// <summary>
///     Moves a single media version's folder to a new root folder on disk and updates its row, without touching
///     any sibling version of the same series or movie. Used by the version-scoped move operation so a
///     multi-version title's other versions are never disturbed.
/// </summary>
public sealed class MoveMediaVersionCommandHandler(SubmarineDbContext db, IEventBus eventBus)
	: ICommandHandler<MoveMediaVersionCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(MoveMediaVersionCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var version = await db.MediaVersions.FirstOrDefaultAsync(x => x.Id == command.MediaVersionId, cancellationToken)
			?? throw new KeyNotFoundException($"Media version {command.MediaVersionId} not found");
		var targetRoot = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == command.RootFolderId, cancellationToken)
			?? throw new KeyNotFoundException($"Root folder {command.RootFolderId} not found");

		if (version.RootFolderId != targetRoot.Id)
		{
			var oldRoot = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
			if (oldRoot is not null)
			{
				var source = MediaVersionPathGuard.ResolveUnderRoot(oldRoot.Path, version.Path);
				var destination = MediaVersionPathGuard.ResolveUnderRoot(targetRoot.Path, version.Path);
				if (Directory.Exists(source) && !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(targetRoot.Path);
					if (Directory.Exists(destination))
					{
						throw new ConflictException($"Destination folder '{destination}' already exists");
					}

					VersionFolderMove.MoveWithFallback(source, destination);
					version.RootFolderId = targetRoot.Id;
					await db.SaveChangesAsync(cancellationToken);
					await eventBus.PublishAsync(new MediaRenamedEvent(version.SeriesId, version.MovieId, [new RenamedFile(source, destination)]), cancellationToken);
					await context.ReportProgressAsync(100, null, cancellationToken);
					return;
				}
			}

			version.RootFolderId = targetRoot.Id;
			await db.SaveChangesAsync(cancellationToken);
		}

		await context.ReportProgressAsync(100, null, cancellationToken);
	}
}

/// <summary>
///     Moves a version folder to a new root, falling back to copy-then-delete when the root folders live on
///     different filesystems (Directory.Move cannot cross a filesystem boundary).
/// </summary>
file static class VersionFolderMove
{
	public static void MoveWithFallback(string source, string destination)
	{
		try
		{
			Directory.Move(source, destination);
		}
		catch (IOException)
		{
			CopyDirectory(source, destination);
			Directory.Delete(source, recursive: true);
		}
	}

	private static void CopyDirectory(string source, string destination)
	{
		Directory.CreateDirectory(destination);
		foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(source, file);
			var target = Path.Combine(destination, relative);
			var targetDirectory = Path.GetDirectoryName(target);
			if (!string.IsNullOrEmpty(targetDirectory))
			{
				Directory.CreateDirectory(targetDirectory);
			}

			File.Copy(file, target, overwrite: true);
		}
	}
}
