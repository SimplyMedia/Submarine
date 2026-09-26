using System.Xml.Linq;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata.Consumers;

/// <summary>
///     WDTV compatible metadata: "{file}.xml" descriptors and folder.jpg cover images.
/// </summary>
public sealed class WdtvMetadataConsumer : IMetadataConsumer
{
	/// <inheritdoc />
	public MetadataConsumerType Type => MetadataConsumerType.WDTV;

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		if (!options.EpisodeMetadata || episodes.Count == 0)
		{
			return [];
		}

		var episode = episodes[0];
		var details = new XElement(
			"details",
			new XElement("id", episode.Id),
			new XElement("title", $"{series.Title} - {episode.SeasonNumber}x{episode.EpisodeNumber:00} - {episode.Title}"),
			new XElement("series_name", series.Title),
			new XElement("episode_name", episode.Title ?? string.Empty),
			new XElement("season_number", episode.SeasonNumber.ToString("00")),
			new XElement("episode_number", episode.EpisodeNumber.ToString("00")),
			new XElement("firstaired", episode.AirDate ?? string.Empty),
			new XElement("genre", string.Join(" / ", series.Genres)),
			new XElement("overview", episode.Overview ?? string.Empty));

		var path = Path.ChangeExtension(episodeFileRelativePath, ".xml");
		return [new MetadataFileResult(path, new XDocument(details).ToString())];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		if (!options.MovieMetadata)
		{
			return [];
		}

		var details = new XElement(
			"details",
			new XElement("id", movie.Id),
			new XElement("title", movie.Title),
			new XElement("year", movie.Year ?? 0),
			new XElement("genre", string.Join(" / ", movie.Genres)),
			new XElement("overview", movie.Overview ?? string.Empty));

		var path = Path.ChangeExtension(movieFileRelativePath, ".xml");
		return [new MetadataFileResult(path, new XDocument(details).ToString())];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeriesImages(Series series, string seriesFolderName, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		return !options.SeriesImages || series.PosterUrl is not { } poster ? [] : [new MetadataImageResult("folder.jpg", poster)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeasonImages(Series series, int seasonNumber, string? seasonFolderRelativePath, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		if (!options.SeasonImages || series.PosterUrl is not { } poster || seasonFolderRelativePath is null)
		{
			return [];
		}

		return [new MetadataImageResult(Path.Combine(seasonFolderRelativePath, "folder.jpg"), poster)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> EpisodeImages(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, string? episodeImageUrl, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		return !options.EpisodeImages || episodeImageUrl is null ? [] : [new MetadataImageResult(Path.ChangeExtension(episodeFileRelativePath, ".metathumb"), episodeImageUrl)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> MovieImages(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (WdtvMetadataConsumerSettings)settings;
		if (!options.MovieImages || movie.PosterUrl is not { } poster)
		{
			return [];
		}

		var directory = Path.GetDirectoryName(movieFileRelativePath) ?? string.Empty;
		return [new MetadataImageResult(Path.Combine(directory, "folder.jpg"), poster)];
	}
}
