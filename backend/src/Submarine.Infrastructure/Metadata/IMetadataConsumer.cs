using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata;

/// <summary>A companion metadata file to write, path relative to the version folder.</summary>
public sealed record MetadataFileResult(string RelativePath, string Content);

/// <summary>A companion image to download, path relative to the version folder.</summary>
public sealed record MetadataImageResult(string RelativePath, string SourceUrl);

/// <summary>
///     Writes companion metadata files and images for one external media center, driven by its own settings.
///     Series methods apply to Kodi, Plex, Roksbox and Wdtv; movie methods apply to Kodi, Emby, Roksbox and Wdtv.
///     Implementations that do not support a given kind return an empty list.
/// </summary>
public interface IMetadataConsumer
{
	/// <summary>Implementation this consumer belongs to.</summary>
	MetadataConsumerType Type { get; }

	/// <summary>
	///     Series level metadata file, written once per series version folder.
	///     <paramref name="versionEpisodeFiles" /> are the episode files currently in that folder, each with
	///     <see cref="EpisodeFile.Episodes" /> loaded, for consumers that enumerate the whole folder (for example Plex).
	/// </summary>
	IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings);

	/// <summary>Metadata file(s) describing the episode(s) contained in one episode file.</summary>
	IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings);

	/// <summary>Metadata file describing a movie file.</summary>
	IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings);

	/// <summary>
	///     Series level images (poster, fanart, banner), written once per series folder.
	///     <paramref name="seriesFolderName" /> is the leaf name of the version folder, used by consumers that
	///     name the series image after the folder (for example Roksbox).
	/// </summary>
	IReadOnlyList<MetadataImageResult> SeriesImages(Series series, string seriesFolderName, MetadataConsumerSettings settings);

	/// <summary>Season level images. <paramref name="seasonFolderRelativePath" /> is null when episodes are not in season folders.</summary>
	IReadOnlyList<MetadataImageResult> SeasonImages(Series series, int seasonNumber, string? seasonFolderRelativePath, MetadataConsumerSettings settings);

	/// <summary>Episode level image(s), typically a screenshot thumbnail. <paramref name="episodeImageUrl" /> is null when none is available.</summary>
	IReadOnlyList<MetadataImageResult> EpisodeImages(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, string? episodeImageUrl, MetadataConsumerSettings settings);

	/// <summary>Movie level images (poster, fanart), written next to the movie file.</summary>
	IReadOnlyList<MetadataImageResult> MovieImages(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings);
}
