using System.Xml.Linq;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Writes Kodi compatible NFO metadata files alongside imported media.
/// </summary>
public interface INfoWriter
{
	/// <summary>Writes "{videoFileNameWithoutExtension}.nfo" describing the episode(s) in a file.</summary>
	void WriteEpisodeNfo(string videoFilePath, IReadOnlyList<Episode> episodes);

	/// <summary>Writes "{videoFileNameWithoutExtension}.nfo" describing a movie.</summary>
	void WriteMovieNfo(string videoFilePath, Movie movie);

	/// <summary>Writes "tvshow.nfo" in the series folder.</summary>
	void WriteSeriesNfo(string seriesFolderPath, Series series);
}

/// <inheritdoc cref="INfoWriter" />
public sealed class NfoWriter : INfoWriter
{
	/// <inheritdoc />
	public void WriteEpisodeNfo(string videoFilePath, IReadOnlyList<Episode> episodes)
	{
		if (episodes.Count == 0)
		{
			return;
		}

		var first = episodes[0];
		var root = new XElement(
			"episodedetails",
			new XElement("title", first.Title ?? string.Empty),
			new XElement("season", first.SeasonNumber),
			new XElement("episode", first.EpisodeNumber),
			new XElement("plot", first.Overview ?? string.Empty),
			new XElement("aired", first.AirDate ?? string.Empty));

		if (first.TvdbId is { } tvdbId)
		{
			root.Add(new XElement("uniqueid", new XAttribute("type", "tvdb"), tvdbId));
		}

		Write(root, Path.ChangeExtension(videoFilePath, ".nfo"));
	}

	/// <inheritdoc />
	public void WriteMovieNfo(string videoFilePath, Movie movie)
	{
		var root = new XElement(
			"movie",
			new XElement("title", movie.Title),
			new XElement("originaltitle", movie.OriginalTitle ?? movie.Title),
			new XElement("year", movie.Year ?? 0),
			new XElement("plot", movie.Overview ?? string.Empty),
			new XElement("uniqueid", new XAttribute("type", "tmdb"), movie.TmdbId));

		Write(root, Path.ChangeExtension(videoFilePath, ".nfo"));
	}

	/// <inheritdoc />
	public void WriteSeriesNfo(string seriesFolderPath, Series series)
	{
		var root = new XElement(
			"tvshow",
			new XElement("title", series.Title),
			new XElement("plot", series.Overview ?? string.Empty),
			new XElement("year", series.Year ?? 0),
			new XElement("uniqueid", new XAttribute("type", "tvdb"), series.TvdbId));

		Write(root, Path.Combine(seriesFolderPath, "tvshow.nfo"));
	}

	private static void Write(XElement root, string path)
	{
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		new XDocument(root).Save(path);
	}
}
