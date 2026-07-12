using System.Xml.Linq;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class NfoWriterServiceTest : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

	public NfoWriterServiceTest()
		=> Directory.CreateDirectory(_root);

	public void Dispose()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	[Fact]
	public void WriteEpisodeNfo_ShouldContainTitleSeasonAndEpisode()
	{
		var videoPath = Path.Combine(_root, "Show - S01E05 - Episode 5.mkv");
		var episode = new Episode
		{
			SeasonNumber = 1, EpisodeNumber = 5, Title = "Episode 5", Overview = "Overview text",
			AirDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)
		};

		NfoWriterService.WriteEpisodeNfo(videoPath, new[] { episode });

		var nfoPath = Path.Combine(_root, "Show - S01E05 - Episode 5.nfo");
		Assert.True(File.Exists(nfoPath));

		var doc = XDocument.Parse(File.ReadAllText(nfoPath));
		Assert.Equal("Episode 5", doc.Root!.Element("title")!.Value);
		Assert.Equal("1", doc.Root.Element("season")!.Value);
		Assert.Equal("5", doc.Root.Element("episode")!.Value);
		Assert.Equal("2024-03-01", doc.Root.Element("aired")!.Value);
	}

	[Fact]
	public void WriteMovieNfo_ShouldContainTitleYearAndUniqueIds()
	{
		var videoPath = Path.Combine(_root, "Movie (2024).mkv");
		var movie = new Movie { Title = "Movie", Year = 2024, Overview = "Overview", TmdbId = 42, ImdbId = "tt123" };

		NfoWriterService.WriteMovieNfo(videoPath, movie);

		var doc = XDocument.Load(Path.Combine(_root, "Movie (2024).nfo"));
		Assert.Equal("Movie", doc.Root!.Element("title")!.Value);
		Assert.Equal("2024", doc.Root.Element("year")!.Value);
		Assert.Contains(doc.Root.Elements("uniqueid"), e => e.Attribute("type")!.Value == "tmdb" && e.Value == "42");
		Assert.Contains(doc.Root.Elements("uniqueid"), e => e.Attribute("type")!.Value == "imdb" && e.Value == "tt123");
	}

	[Fact]
	public void WriteTvShowNfo_ShouldContainTitleUniqueIdsAndGenreFromTags()
	{
		var series = new Series
		{
			TvdbId = 99, TmdbId = 7, Title = "Show", Year = 2020, Overview = "Overview",
			Tags = new List<string> { "Drama", "Thriller" }
		};

		NfoWriterService.WriteTvShowNfo(_root, series);

		var doc = XDocument.Load(Path.Combine(_root, "tvshow.nfo"));
		Assert.Equal("Show", doc.Root!.Element("title")!.Value);
		Assert.Contains(doc.Root.Elements("uniqueid"), e => e.Attribute("type")!.Value == "tvdb" && e.Value == "99");
		Assert.Contains(doc.Root.Elements("uniqueid"), e => e.Attribute("type")!.Value == "tmdb" && e.Value == "7");
		Assert.Equal(new[] { "Drama", "Thriller" }, doc.Root.Elements("genre").Select(e => e.Value));
	}
}
