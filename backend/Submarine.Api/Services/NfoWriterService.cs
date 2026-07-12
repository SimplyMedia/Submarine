using System.Xml.Linq;
using Submarine.Core.Library;

namespace Submarine.Api.Services;

/// <summary>
///     Writes Kodi-style NFO metadata sidecar files
/// </summary>
public static class NfoWriterService
{
	/// <summary>
	///     Factory used to resolve the "metadata-images" http client for poster/fanart downloads. Configured once at
	///     startup; when null, image downloads are skipped.
	/// </summary>
	public static IHttpClientFactory? HttpClientFactory { get; set; }

	/// <summary>
	///     Logger used to record image download failures at debug level; when null, failures are swallowed silently.
	/// </summary>
	public static ILogger? Logger { get; set; }

	/// <summary>
	///     Writes {episodefilename}.nfo next to <paramref name="videoPath" />, one episodedetails block per episode
	///     the file satisfies, overwriting any existing file
	/// </summary>
	public static void WriteEpisodeNfo(string videoPath, IReadOnlyList<Episode> episodes)
	{
		var path = Path.ChangeExtension(videoPath, ".nfo");
		var content = string.Join("", episodes.Select(e => BuildEpisodeDocument(e).ToString()));

		File.WriteAllText(path, content);
	}

	/// <summary>
	///     Writes {moviefilename}.nfo next to <paramref name="videoPath" />, overwriting any existing file, and downloads
	///     poster/fanart artwork into the movie folder when it is not already present
	/// </summary>
	public static void WriteMovieNfo(string videoPath, Movie movie)
	{
		var path = Path.ChangeExtension(videoPath, ".nfo");
		var root = new XElement("movie",
			new XElement("title", movie.Title),
			new XElement("year", movie.Year),
			new XElement("plot", movie.Overview));

		if (movie.TmdbId > 0)
			root.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), new XAttribute("default", "true"),
				movie.TmdbId));

		if (!string.IsNullOrEmpty(movie.ImdbId))
			root.Add(new XElement("uniqueid", new XAttribute("type", "imdb"), movie.ImdbId));

		new XDocument(root).Save(path);

		DownloadImages(Path.GetDirectoryName(path)!, movie.PosterUrl, movie.BackdropUrl);
	}

	/// <summary>
	///     Writes tvshow.nfo in the series version folder, overwriting any existing file, and downloads poster/fanart
	///     artwork into the version folder when it is not already present
	/// </summary>
	public static void WriteTvShowNfo(string versionPath, Series series)
	{
		var path = Path.Combine(versionPath, "tvshow.nfo");
		var root = new XElement("tvshow",
			new XElement("title", series.Title),
			new XElement("year", series.Year),
			new XElement("plot", series.Overview));

		if (series.TvdbId > 0)
			root.Add(new XElement("uniqueid", new XAttribute("type", "tvdb"), new XAttribute("default", "true"),
				series.TvdbId));

		if (series.TmdbId != null)
			root.Add(new XElement("uniqueid", new XAttribute("type", "tmdb"), series.TmdbId));

		foreach (var tag in series.Tags)
			root.Add(new XElement("genre", tag));

		new XDocument(root).Save(path);

		DownloadImages(versionPath, series.PosterUrl, series.BackdropUrl);
	}

	private static XDocument BuildEpisodeDocument(Episode episode)
		=> new(new XElement("episodedetails",
			new XElement("title", episode.Title),
			new XElement("season", episode.SeasonNumber),
			new XElement("episode", episode.EpisodeNumber),
			new XElement("aired", episode.AirDate?.ToString("yyyy-MM-dd")),
			new XElement("plot", episode.Overview)));

	private static void DownloadImages(string folder, string? posterUrl, string? backdropUrl)
	{
		if (HttpClientFactory == null)
			return;

		DownloadImage(folder, "poster.jpg", posterUrl);
		DownloadImage(folder, "fanart.jpg", backdropUrl);
	}

	// Best-effort: a missing url, an existing file, or any transport/status failure never blocks the import
	private static void DownloadImage(string folder, string fileName, string? url)
	{
		if (string.IsNullOrEmpty(url))
			return;

		var target = Path.Combine(folder, fileName);

		if (File.Exists(target))
			return;

		try
		{
			using var client = HttpClientFactory!.CreateClient("metadata-images");
			using var response = client.GetAsync(url).GetAwaiter().GetResult();

			if (!response.IsSuccessStatusCode)
				return;

			var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

			File.WriteAllBytes(target, bytes);
		}
		catch (Exception ex)
		{
			Logger?.LogDebug(ex, "Failed to download {File} from {Url}", fileName, url);
		}
	}
}
