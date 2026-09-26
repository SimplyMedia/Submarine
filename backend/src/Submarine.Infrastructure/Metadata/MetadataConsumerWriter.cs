using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Entities;
using Submarine.Core.Metadata;
using Submarine.Core.Naming;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Writes companion metadata files and images for every enabled <see cref="MetadataConsumer" /> as media is
///     imported, rescanned or renamed.
/// </summary>
public interface IMetadataConsumerWriter
{
	/// <summary>Writes series and episode level metadata for one imported or adopted episode file.</summary>
	Task WriteEpisodeAsync(Series series, int mediaVersionId, string versionFolder, EpisodeFile file, IReadOnlyList<Episode> episodes, CancellationToken cancellationToken);

	/// <summary>Writes movie level metadata for one imported or adopted movie file.</summary>
	Task WriteMovieAsync(Movie movie, string versionFolder, MovieFile file, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class MetadataConsumerWriter(
	SubmarineDbContext db,
	IMetadataConsumerFactory factory,
	IMetadataConsumerImageDownloader imageDownloader,
	IMetadataClientImageResolver episodeImageResolver,
	NamingService namingService,
	ILogger<MetadataConsumerWriter> logger) : IMetadataConsumerWriter
{
	/// <inheritdoc />
	public async Task WriteEpisodeAsync(Series series, int mediaVersionId, string versionFolder, EpisodeFile file, IReadOnlyList<Episode> episodes, CancellationToken cancellationToken)
	{
		var consumers = await db.MetadataConsumers.AsNoTracking().Where(x => x.Enable).ToListAsync(cancellationToken);
		if (consumers.Count == 0)
		{
			return;
		}

		var versionEpisodeFiles = await db.EpisodeFiles.AsNoTracking()
			.Include(x => x.Episodes)
			.Where(x => x.MediaVersionId == mediaVersionId)
			.ToListAsync(cancellationToken);

		var seriesFolderName = Path.GetFileName(versionFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var seasonNumber = episodes.Count > 0 ? episodes[0].SeasonNumber : 0;
		var seasonFolderRelativePath = series.SeasonFolder ? namingService.RenderSeasonFolder(await NamingConfigAsync(cancellationToken), series, seasonNumber) : null;
		string? episodeImageUrl = null;

		foreach (var consumer in consumers)
		{
			var settings = MetadataConsumerSettingsJson.Parse(consumer.Type, consumer.SettingsJson);
			var implementation = factory.Resolve(consumer.Type);

			await WriteFilesAsync(versionFolder, implementation.SeriesMetadata(series, versionEpisodeFiles, settings), cancellationToken);
			await WriteFilesAsync(versionFolder, implementation.EpisodeMetadata(series, episodes, file.RelativePath, settings), cancellationToken);
			await imageDownloader.DownloadAsync(versionFolder, implementation.SeriesImages(series, seriesFolderName, settings), cancellationToken);
			await imageDownloader.DownloadAsync(versionFolder, implementation.SeasonImages(series, seasonNumber, seasonFolderRelativePath, settings), cancellationToken);

			episodeImageUrl ??= await episodeImageResolver.ResolveEpisodeImageUrlAsync(series, episodes, cancellationToken);
			await imageDownloader.DownloadAsync(versionFolder, implementation.EpisodeImages(series, episodes, file.RelativePath, episodeImageUrl, settings), cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task WriteMovieAsync(Movie movie, string versionFolder, MovieFile file, CancellationToken cancellationToken)
	{
		var consumers = await db.MetadataConsumers.AsNoTracking().Where(x => x.Enable).ToListAsync(cancellationToken);
		if (consumers.Count == 0)
		{
			return;
		}

		foreach (var consumer in consumers)
		{
			var settings = MetadataConsumerSettingsJson.Parse(consumer.Type, consumer.SettingsJson);
			var implementation = factory.Resolve(consumer.Type);

			await WriteFilesAsync(versionFolder, implementation.MovieMetadata(movie, file.RelativePath, settings), cancellationToken);
			await imageDownloader.DownloadAsync(versionFolder, implementation.MovieImages(movie, file.RelativePath, settings), cancellationToken);
		}
	}

	private async Task<NamingConfig> NamingConfigAsync(CancellationToken cancellationToken)
		=> await db.NamingConfig.AsNoTracking().SingleAsync(cancellationToken);

	private async Task WriteFilesAsync(string versionFolder, IReadOnlyList<MetadataFileResult> files, CancellationToken cancellationToken)
	{
		foreach (var file in files)
		{
			try
			{
				var path = Path.Combine(versionFolder, file.RelativePath);
				var directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				await File.WriteAllTextAsync(path, file.Content, cancellationToken);
			}
			catch (IOException exception)
			{
				logger.LogWarning(exception, "Unable to write metadata file {RelativePath}", file.RelativePath);
			}
		}
	}
}
