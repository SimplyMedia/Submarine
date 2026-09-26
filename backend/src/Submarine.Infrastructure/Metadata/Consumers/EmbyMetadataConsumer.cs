using System.Xml.Linq;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata.Consumers;

/// <summary>
///     Emby legacy movie.xml metadata, movies only. Superseded upstream by the shared Kodi NFO format, kept for
///     older Emby installs that still read it.
/// </summary>
public sealed class EmbyMetadataConsumer : IMetadataConsumer
{
	/// <inheritdoc />
	public MetadataConsumerType Type => MetadataConsumerType.EMBY;

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (EmbyMetadataConsumerSettings)settings;
		if (!options.MovieMetadata)
		{
			return [];
		}

		var details = new XElement(
			"Movie",
			new XElement("id", movie.ImdbId ?? string.Empty),
			new XElement("Status", movie.Status),
			new XElement("Added", movie.CreatedAt.ToString("MM/dd/yyyy HH:mm:ss")),
			new XElement("LockData", "false"),
			new XElement("Overview", movie.Overview ?? string.Empty),
			new XElement("LocalTitle", movie.Title),
			new XElement("ProductionYear", movie.Year ?? 0),
			new XElement("RunningTime", movie.Runtime ?? 0),
			new XElement("IMDB", movie.ImdbId ?? string.Empty),
			new XElement("Genres", movie.Genres.Select(genre => new XElement("Genre", genre))));

		var directory = Path.GetDirectoryName(movieFileRelativePath) ?? string.Empty;
		return [new MetadataFileResult(Path.Combine(directory, "movie.xml"), new XDocument(details).ToString())];
	}

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
