using System.Text;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata.Consumers;

/// <summary>
///     Plex .plexmatch file, series only. Movies and season/episode images are not supported by Plex matching.
/// </summary>
public sealed class PlexMetadataConsumer : IMetadataConsumer
{
	/// <inheritdoc />
	public MetadataConsumerType Type => MetadataConsumerType.PLEX;

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings)
	{
		var options = (PlexMetadataConsumerSettings)settings;
		if (!options.SeriesPlexMatchFile)
		{
			return [];
		}

		var content = new StringBuilder();
		content.AppendLine($"Title: {series.Title}");
		content.AppendLine($"Year: {series.Year}");
		content.AppendLine($"TvdbId: {series.TvdbId}");
		content.AppendLine($"ImdbId: {series.ImdbId}");

		if (options.EpisodeMappings)
		{
			foreach (var file in versionEpisodeFiles.OrderBy(f => f.Episodes.Min(e => e.SeasonNumber)).ThenBy(f => f.Episodes.Min(e => e.EpisodeNumber)))
			{
				var episodes = file.Episodes.OrderBy(e => e.EpisodeNumber).ToList();
				if (episodes.Count == 0)
				{
					continue;
				}

				var seasonNumber = episodes[0].SeasonNumber;
				var episodeFormat = seasonNumber == 0
					? $"SP{episodes[0].EpisodeNumber:00}"
					: $"S{seasonNumber:00}{string.Join(string.Empty, episodes.Select(e => $"E{e.EpisodeNumber:00}"))}";

				content.AppendLine($"Episode: {episodeFormat}: {file.RelativePath}");
			}
		}

		return [new MetadataFileResult(".plexmatch", content.ToString())];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeriesImages(Series series, string seriesFolderName, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeasonImages(Series series, int seasonNumber, string? seasonFolderRelativePath, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> EpisodeImages(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, string? episodeImageUrl, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> MovieImages(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
		=> [];
}
