using System.Xml.Linq;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;

namespace Submarine.Infrastructure.Metadata.Consumers;

/// <summary>
///     Kodi/Emby compatible NFO metadata: tvshow.nfo, "{file}.nfo" and cover images.
/// </summary>
public sealed class KodiMetadataConsumer : IMetadataConsumer
{
	/// <inheritdoc />
	public MetadataConsumerType Type => MetadataConsumerType.KODI;

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> SeriesMetadata(Series series, IReadOnlyList<EpisodeFile> versionEpisodeFiles, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		var content = string.Empty;

		if (options.SeriesMetadata)
		{
			var tvShow = new XElement(
				"tvshow",
				new XElement("title", series.Title),
				new XElement("plot", series.Overview ?? string.Empty),
				new XElement("mpaa", series.Certification ?? string.Empty),
				new XElement("uniqueid", new XAttribute("type", "tvdb"), new XAttribute("default", "true"), series.TvdbId));

			if (!string.IsNullOrWhiteSpace(series.ImdbId))
			{
				tvShow.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), series.ImdbId));
			}

			if (series.TmdbId is { } tmdbId)
			{
				tvShow.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), tmdbId));
			}

			foreach (var genre in series.Genres)
			{
				tvShow.Add(new XElement("genre", genre));
			}

			foreach (var tag in series.Tags)
			{
				tvShow.Add(new XElement("tag", tag.Label));
			}

			tvShow.Add(new XElement("status", series.Status));

			if (series.FirstAired is { } firstAired)
			{
				tvShow.Add(new XElement("premiered", firstAired.ToString("yyyy-MM-dd")));
			}

			tvShow.Add(new XElement("studio", series.Network ?? string.Empty));

			content = new XDocument(tvShow).ToString();
		}

		if (options.SeriesMetadataUrl)
		{
			if (content.Length > 0)
			{
				content += Environment.NewLine;
			}

			content += $"https://www.thetvdb.com/?tab=series&id={series.TvdbId}";
		}

		return content.Length == 0 ? [] : [new MetadataFileResult("tvshow.nfo", content)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> EpisodeMetadata(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		if (!options.EpisodeMetadata || episodes.Count == 0)
		{
			return [];
		}

		var fragments = new List<string>();
		foreach (var episode in episodes)
		{
			var details = new XElement(
				"episodedetails",
				new XElement("title", episode.Title ?? string.Empty),
				new XElement("season", episode.SeasonNumber),
				new XElement("episode", episode.EpisodeNumber),
				new XElement("aired", episode.AirDate ?? string.Empty),
				new XElement("plot", episode.Overview ?? string.Empty));

			if (episode.TvdbId is { } tvdbId)
			{
				details.Add(new XElement("uniqueid", new XAttribute("type", "tvdb"), new XAttribute("default", "true"), tvdbId));
			}

			details.Add(new XElement("uniqueid", new XAttribute("type", "submarine"), episode.Id));

			fragments.Add(details.ToString());
		}

		var path = Path.ChangeExtension(episodeFileRelativePath, ".nfo");
		return [new MetadataFileResult(path, string.Join(Environment.NewLine, fragments))];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataFileResult> MovieMetadata(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		var content = string.Empty;

		if (options.MovieMetadata)
		{
			var details = new XElement(
				"movie",
				new XElement("title", movie.Title),
				new XElement("originaltitle", movie.OriginalTitle ?? movie.Title),
				new XElement("year", movie.Year ?? 0),
				new XElement("plot", movie.Overview ?? string.Empty),
				new XElement("runtime", movie.Runtime ?? 0),
				new XElement("uniqueid", new XAttribute("type", "tmdb"), new XAttribute("default", "true"), movie.TmdbId));

			if (!string.IsNullOrWhiteSpace(movie.ImdbId))
			{
				details.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), movie.ImdbId));
			}

			foreach (var genre in movie.Genres)
			{
				details.Add(new XElement("genre", genre));
			}

			if (!string.IsNullOrWhiteSpace(movie.Certification))
			{
				details.Add(new XElement("mpaa", movie.Certification));
			}

			if (movie.InCinemasDate is { } inCinemas)
			{
				details.Add(new XElement("premiered", inCinemas.ToString("yyyy-MM-dd")));
			}

			details.Add(new XElement("studio", movie.Studio ?? string.Empty));

			if (!string.IsNullOrWhiteSpace(movie.CollectionTitle))
			{
				details.Add(new XElement("set", new XElement("name", movie.CollectionTitle)));
			}

			content = new XDocument(details).ToString();
		}

		if (options.MovieMetadataUrl)
		{
			if (content.Length > 0)
			{
				content += Environment.NewLine;
			}

			content += $"https://www.themoviedb.org/movie/{movie.TmdbId}";
			if (!string.IsNullOrWhiteSpace(movie.ImdbId))
			{
				content += Environment.NewLine + $"https://www.imdb.com/title/{movie.ImdbId}";
			}
		}

		var path = Path.ChangeExtension(movieFileRelativePath, ".nfo");
		return content.Length == 0 ? [] : [new MetadataFileResult(path, content)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeriesImages(Series series, string seriesFolderName, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		if (!options.SeriesImages)
		{
			return [];
		}

		var results = new List<MetadataImageResult>();
		if (series.PosterUrl is { } poster)
		{
			results.Add(new MetadataImageResult("poster.jpg", poster));
		}

		if (series.BackdropUrl is { } backdrop)
		{
			results.Add(new MetadataImageResult("fanart.jpg", backdrop));
		}

		return results;
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> SeasonImages(Series series, int seasonNumber, string? seasonFolderRelativePath, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		if (!options.SeasonImages || series.PosterUrl is not { } poster)
		{
			return [];
		}

		var suffix = seasonNumber == 0 ? "specials" : seasonNumber.ToString("00");
		return [new MetadataImageResult($"season{suffix}-poster.jpg", poster)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> EpisodeImages(Series series, IReadOnlyList<Episode> episodes, string episodeFileRelativePath, string? episodeImageUrl, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		if (!options.EpisodeImages || episodeImageUrl is null)
		{
			return [];
		}

		var stem = Path.Combine(Path.GetDirectoryName(episodeFileRelativePath) ?? string.Empty, Path.GetFileNameWithoutExtension(episodeFileRelativePath));
		return [new MetadataImageResult($"{stem}-thumb.jpg", episodeImageUrl)];
	}

	/// <inheritdoc />
	public IReadOnlyList<MetadataImageResult> MovieImages(Movie movie, string movieFileRelativePath, MetadataConsumerSettings settings)
	{
		var options = (KodiMetadataConsumerSettings)settings;
		if (!options.MovieImages)
		{
			return [];
		}

		var results = new List<MetadataImageResult>();
		if (movie.PosterUrl is { } poster)
		{
			results.Add(new MetadataImageResult("poster.jpg", poster));
		}

		if (movie.BackdropUrl is { } backdrop)
		{
			results.Add(new MetadataImageResult("fanart.jpg", backdrop));
		}

		return results;
	}
}
