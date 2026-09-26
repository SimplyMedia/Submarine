using System.Xml.Linq;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata.Consumers;

/// <summary>
///     Roksbox compatible metadata: "{file}.xml" video descriptors and images named after their folders.
/// </summary>
public sealed class RoksboxMetadataConsumer : IMetadataConsumer
{
	private static readonly string[] ValidCertifications =
		["G", "NC-17", "PG", "PG-13", "R", "UR", "UNRATED", "NR", "TV-Y", "TV-Y7", "TV-Y7-FV", "TV-G", "TV-PG", "TV-14", "TV-MA"];

	/// <inheritdoc />
	public MetadataConsumerType Type => MetadataConsumerType.ROKSBOX;

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings)
		=> [];

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		if (!options.EpisodeMetadata || episodes.Count == 0)
		{
			return [];
		}

		var episode = episodes[0];
		var mpaa = series.Certification is { } certification && ValidCertifications.Contains(certification.ToUpperInvariant())
			? certification.ToUpperInvariant()
			: "UNRATED";

		var video = new XElement(
			"video",
			new XElement("title", $"{series.Title} - {episode.SeasonNumber}x{episode.EpisodeNumber} - {episode.Title}"),
			new XElement("year", episode.AirDate ?? string.Empty),
			new XElement("genre", string.Join(" / ", series.Genres)),
			new XElement("description", episode.Overview ?? string.Empty),
			new XElement("length", series.Runtime ?? 0),
			new XElement("mpaa", mpaa));

		var path = Path.ChangeExtension(episodeFileRelativePath, ".xml");
		return [new MetadataFileResult(path, new XDocument(video).ToString())];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		if (!options.MovieMetadata)
		{
			return [];
		}

		var mpaa = movie.Certification is { } certification && ValidCertifications.Contains(certification.ToUpperInvariant())
			? certification.ToUpperInvariant()
			: "UNRATED";

		var video = new XElement(
			"video",
			new XElement("title", movie.Title),
			new XElement("year", movie.Year ?? 0),
			new XElement("genre", string.Join(" / ", movie.Genres)),
			new XElement("description", movie.Overview ?? string.Empty),
			new XElement("length", movie.Runtime ?? 0),
			new XElement("mpaa", mpaa));

		var path = Path.ChangeExtension(movieFileRelativePath, ".xml");
		return [new MetadataFileResult(path, new XDocument(video).ToString())];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeriesImages(Series series, string seriesFolderName, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		return !options.SeriesImages || series.PosterUrl is not { } poster ? [] : [new MetadataImageResult($"{seriesFolderName}.jpg", poster)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeasonImages(Series series, int seasonNumber, string? seasonFolderRelativePath, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		if (!options.SeasonImages || series.PosterUrl is not { } poster || seasonFolderRelativePath is null)
		{
			return [];
		}

		var folderName = Path.GetFileName(seasonFolderRelativePath);
		return [new MetadataImageResult(Path.Combine(seasonFolderRelativePath, $"{folderName}.jpg"), poster)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> EpisodeImages(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, string? episodeImageUrl, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		return !options.EpisodeImages || episodeImageUrl is null ? [] : [new MetadataImageResult(Path.ChangeExtension(episodeFileRelativePath, ".jpg"), episodeImageUrl)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> MovieImages(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (RoksboxMetadataConsumerSettings)settings;
		if (!options.MovieImages)
		{
			return [];
		}

		var stem = Path.Combine(Path.GetDirectoryName(movieFileRelativePath) ?? string.Empty, Path.GetFileNameWithoutExtension(movieFileRelativePath));
		var results = new List<MetadataImageResult>();
		if (movie.PosterUrl is { } poster)
		{
			results.Add(new MetadataImageResult($"{stem}-poster.jpg", poster));
		}

		if (movie.BackdropUrl is { } backdrop)
		{
			results.Add(new MetadataImageResult($"{stem}-fanart.jpg", backdrop));
		}

		return results;
	}
}
