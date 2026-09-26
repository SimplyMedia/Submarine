using System.Xml.Linq;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

public sealed class NfoWriterTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-nfo-").FullName;
	private readonly NfoWriter _writer = new();

	public void Dispose() => Directory.Delete(_tempDir, recursive: true);

	[Fact]
	public void WriteEpisodeNfo_ShouldWriteEpisodeDetailsXml_NextToVideoFile()
	{
		var videoPath = Path.Combine(_tempDir, "Show - S01E02 - Title.mkv");
		var episode = new Episode { Id = 1, SeriesId = 1, SeasonNumber = 1, EpisodeNumber = 2, Title = "Title", Overview = "Plot", AirDate = "2024-01-01", TvdbId = 555 };

		_writer.WriteEpisodeNfo(videoPath, [episode]);

		var nfoPath = Path.Combine(_tempDir, "Show - S01E02 - Title.nfo");
		File.Exists(nfoPath).ShouldBeTrue();

		var doc = XDocument.Load(nfoPath);
		doc.Root!.Name.LocalName.ShouldBe("episodedetails");
		doc.Root.Element("title")!.Value.ShouldBe("Title");
		doc.Root.Element("season")!.Value.ShouldBe("1");
		doc.Root.Element("episode")!.Value.ShouldBe("2");
		doc.Root.Element("plot")!.Value.ShouldBe("Plot");
		doc.Root.Element("uniqueid")!.Value.ShouldBe("555");
	}

	[Fact]
	public void WriteEpisodeNfo_ShouldNoOp_WhenNoEpisodesGiven()
	{
		var videoPath = Path.Combine(_tempDir, "video.mkv");

		_writer.WriteEpisodeNfo(videoPath, []);

		File.Exists(Path.Combine(_tempDir, "video.nfo")).ShouldBeFalse();
	}

	[Fact]
	public void WriteMovieNfo_ShouldWriteMovieXml_NextToVideoFile()
	{
		var videoPath = Path.Combine(_tempDir, "Movie (2020).mkv");
		var movie = new Movie { Id = 1, TmdbId = 42, Title = "Movie", Year = 2020, Overview = "Plot" };

		_writer.WriteMovieNfo(videoPath, movie);

		var doc = XDocument.Load(Path.Combine(_tempDir, "Movie (2020).nfo"));
		doc.Root!.Name.LocalName.ShouldBe("movie");
		doc.Root.Element("title")!.Value.ShouldBe("Movie");
		doc.Root.Element("year")!.Value.ShouldBe("2020");
		doc.Root.Element("uniqueid")!.Value.ShouldBe("42");
	}

	[Fact]
	public void WriteSeriesNfo_ShouldWriteTvShowXml_InSeriesFolder()
	{
		var series = new Series { Id = 1, TvdbId = 99, Title = "Show", Overview = "Overview" };

		_writer.WriteSeriesNfo(_tempDir, series);

		var doc = XDocument.Load(Path.Combine(_tempDir, "tvshow.nfo"));
		doc.Root!.Name.LocalName.ShouldBe("tvshow");
		doc.Root.Element("title")!.Value.ShouldBe("Show");
		doc.Root.Element("uniqueid")!.Value.ShouldBe("99");
	}
}
