using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Renames a series' files and, when the title changed, its folder to match the naming templates.
/// </summary>
public sealed class RenameSeriesCommandHandler(
	SubmarineDbContext db,
	NamingService namingService,
	IMetadataConsumerWriter metadataWriter,
	IEventBus eventBus,
	TimeProvider timeProvider) : ICommandHandler<RenameSeriesCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RenameSeriesCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var series = await db.Series.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == command.SeriesId, cancellationToken)
			?? throw new KeyNotFoundException($"Series {command.SeriesId} not found");
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var renamed = new List<RenamedFile>();
		var renamedEpisodeFiles = new List<(int VersionId, string VersionFolder, EpisodeFile File, List<Episode> Episodes)>();

		foreach (var version in series.Versions)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
			if (root is null)
			{
				continue;
			}

			var expectedFolderName = namingService.RenderSeriesFolder(naming, series);
			var oldVersionFolder = Path.Combine(root.Path, version.Path);
			if (!string.Equals(version.Path, expectedFolderName, StringComparison.Ordinal) && Directory.Exists(oldVersionFolder))
			{
				var newVersionFolder = Path.Combine(root.Path, expectedFolderName);
				if (!Directory.Exists(newVersionFolder))
				{
					Directory.Move(oldVersionFolder, newVersionFolder);
					version.Path = expectedFolderName;
				}
			}

			var versionFolder = Path.Combine(root.Path, version.Path);
			var query = db.EpisodeFiles.Include(x => x.Episodes).Where(x => x.MediaVersionId == version.Id);
			if (command.FileIds is { } fileIds)
			{
				query = query.Where(x => fileIds.Contains(x.Id));
			}

			foreach (var file in await query.ToListAsync(cancellationToken))
			{
				var episodes = file.Episodes.OrderBy(x => x.EpisodeNumber).ToList();
				if (episodes.Count == 0)
				{
					continue;
				}

				var targetFolder = series.SeasonFolder
					? Path.Combine(versionFolder, namingService.RenderSeasonFolder(naming, series, episodes[0].SeasonNumber))
					: versionFolder;
				var extension = Path.GetExtension(file.RelativePath);
				var newFileName = namingService.RenderEpisodeFileName(series, episodes, file, naming) + extension;
				var oldFullPath = Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath));
				var newFullPath = Path.GetFullPath(Path.Combine(targetFolder, newFileName));

				if (string.Equals(oldFullPath, newFullPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(oldFullPath))
				{
					continue;
				}

				if (File.Exists(newFullPath))
				{
					continue;
				}

				Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
				File.Move(oldFullPath, newFullPath);
				file.RelativePath = Path.GetRelativePath(versionFolder, newFullPath);
				file.NamedFromPlaceholder = false;
				renamed.Add(new RenamedFile(oldFullPath, newFullPath));
				renamedEpisodeFiles.Add((version.Id, versionFolder, file, episodes));
			}
		}

		await db.SaveChangesAsync(cancellationToken);

		foreach (var (versionId, versionFolder, file, episodes) in renamedEpisodeFiles)
		{
			await metadataWriter.WriteEpisodeAsync(series, versionId, versionFolder, file, episodes, cancellationToken);
		}

		if (renamed.Count == 0)
		{
			return;
		}

		await eventBus.PublishAsync(new MediaRenamedEvent(series.Id, null, renamed), cancellationToken);
		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.RENAMED,
			SeriesId = series.Id,
			SourceTitle = series.Title,
			Date = timeProvider.GetUtcNow().UtcDateTime,
			Data = JsonSerializer.Serialize(new { count = renamed.Count })
		});
		await db.SaveChangesAsync(cancellationToken);
	}
}

/// <summary>
///     Renames a movie's files and, when the title changed, its folder to match the naming templates.
/// </summary>
public sealed class RenameMovieCommandHandler(
	SubmarineDbContext db,
	NamingService namingService,
	IMetadataConsumerWriter metadataWriter,
	IEventBus eventBus,
	TimeProvider timeProvider) : ICommandHandler<RenameMovieCommand>
{
	/// <inheritdoc />
	public async Task ExecuteAsync(RenameMovieCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var movie = await db.Movies.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == command.MovieId, cancellationToken)
			?? throw new KeyNotFoundException($"Movie {command.MovieId} not found");
		var naming = await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);
		var renamed = new List<RenamedFile>();
		var renamedMovieFiles = new List<(string VersionFolder, MovieFile File)>();

		foreach (var version in movie.Versions)
		{
			var root = await db.RootFolders.FirstOrDefaultAsync(x => x.Id == version.RootFolderId, cancellationToken);
			if (root is null)
			{
				continue;
			}

			var expectedFolderName = namingService.RenderMovieFolder(naming, movie);
			var oldVersionFolder = Path.Combine(root.Path, version.Path);
			if (!string.Equals(version.Path, expectedFolderName, StringComparison.Ordinal) && Directory.Exists(oldVersionFolder))
			{
				var newVersionFolder = Path.Combine(root.Path, expectedFolderName);
				if (!Directory.Exists(newVersionFolder))
				{
					Directory.Move(oldVersionFolder, newVersionFolder);
					version.Path = expectedFolderName;
				}
			}

			var versionFolder = Path.Combine(root.Path, version.Path);
			var query = db.MovieFiles.Where(x => x.MediaVersionId == version.Id);
			if (command.FileIds is { } fileIds)
			{
				query = query.Where(x => fileIds.Contains(x.Id));
			}

			foreach (var file in await query.ToListAsync(cancellationToken))
			{
				var extension = Path.GetExtension(file.RelativePath);
				var newFileName = namingService.RenderMovieFileName(movie, file, naming) + extension;
				var oldFullPath = Path.GetFullPath(Path.Combine(versionFolder, file.RelativePath));
				var newFullPath = Path.GetFullPath(Path.Combine(versionFolder, newFileName));

				if (string.Equals(oldFullPath, newFullPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(oldFullPath))
				{
					continue;
				}

				if (File.Exists(newFullPath))
				{
					continue;
				}

				Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
				File.Move(oldFullPath, newFullPath);
				file.RelativePath = Path.GetRelativePath(versionFolder, newFullPath);
				renamed.Add(new RenamedFile(oldFullPath, newFullPath));
				renamedMovieFiles.Add((versionFolder, file));
			}
		}

		await db.SaveChangesAsync(cancellationToken);

		foreach (var (versionFolder, file) in renamedMovieFiles)
		{
			await metadataWriter.WriteMovieAsync(movie, versionFolder, file, cancellationToken);
		}

		if (renamed.Count == 0)
		{
			return;
		}

		await eventBus.PublishAsync(new MediaRenamedEvent(null, movie.Id, renamed), cancellationToken);
		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.RENAMED,
			MovieId = movie.Id,
			SourceTitle = movie.Title,
			Date = timeProvider.GetUtcNow().UtcDateTime,
			Data = JsonSerializer.Serialize(new { count = renamed.Count })
		});
		await db.SaveChangesAsync(cancellationToken);
	}
}
