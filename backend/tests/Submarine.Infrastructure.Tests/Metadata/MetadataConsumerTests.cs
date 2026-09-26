using System.Xml.Linq;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Metadata;
using Submarine.Infrastructure.Metadata.Consumers;
using Xunit;

namespace Submarine.Infrastructure.Tests.Metadata;

/// <summary>
///     Generated NFO/XML content and file names for each metadata consumer.
/// </summary>
public sealed class MetadataConsumerTests
{
	private static readonly Series TestSeries = new()
	{
		Id = 1,
		TvdbId = 100,
		ImdbId = "tt100",
		Title = "Test Show",
		Overview = "A show",
		Certification = "TV-14",
		Genres = ["Drama", "Comedy"],
		Network = "Test Network",
		Status = SeriesStatus.CONTINUING,
		Year = 2020,
		PosterUrl = "https://example.com/poster.jpg",
		BackdropUrl = "https://example.com/fanart.jpg"
	};

	private static readonly Movie TestMovie = new()
	{
		Id = 1,
		TmdbId = 200,
		ImdbId = "tt200",
		Title = "Test Movie",
		OriginalTitle = "Test Movie",
		Overview = "A movie",
		Certification = "PG-13",
		Genres = ["Action"],
		Studio = "Test Studio",
		Year = 2021,
		Runtime = 120,
		PosterUrl = "https://example.com/movie-poster.jpg",
		BackdropUrl = "https://example.com/movie-fanart.jpg"
	};

	private static Episode TestEpisode(int season = 1, int number = 2)
		=> new() { Id = 5, SeriesId = 1, SeasonNumber = season, EpisodeNumber = number, Title = "Pilot", Overview = "Plot", AirDate = "2020-01-02", TvdbId = 55 };

	[Fact]
	public void Kodi_SeriesMetadata_ShouldWriteTvShowNfo_WithIds()
	{
		var consumer = new KodiMetadataConsumer();
		var result = consumer.SeriesMetadata(TestSeries, [], new KodiMetadataConsumerSettings());

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("tvshow.nfo");
		var doc = XDocument.Parse(result[0].Content);
		doc.Root!.Name.LocalName.ShouldBe("tvshow");
		doc.Root.Element("title")!.Value.ShouldBe("Test Show");
		doc.Root.Elements("uniqueid").ShouldContain(x => x.Attribute("type")!.Value == "tvdb" && x.Value == "100");
	}

	[Fact]
	public void Kodi_SeriesMetadata_ShouldBeEmpty_WhenDisabled()
	{
		var consumer = new KodiMetadataConsumer();
		var result = consumer.SeriesMetadata(TestSeries, [], new KodiMetadataConsumerSettings { SeriesMetadata = false });
		result.ShouldBeEmpty();
	}

	[Fact]
	public void Kodi_EpisodeMetadata_ShouldNameFileAfterVideo_WithNfoExtension()
	{
		var consumer = new KodiMetadataConsumer();
		var result = consumer.EpisodeMetadata(TestSeries, [TestEpisode()], "Season 01/Test Show - S01E02.mkv", new KodiMetadataConsumerSettings());

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("Season 01/Test Show - S01E02.nfo");
		result[0].Content.ShouldContain("<episodedetails>");
		result[0].Content.ShouldContain("<title>Pilot</title>");
	}

	[Fact]
	public void Kodi_MovieMetadata_ShouldIncludeMetadataUrls_WhenEnabled()
	{
		var consumer = new KodiMetadataConsumer();
		var result = consumer.MovieMetadata(TestMovie, "Test Movie (2021).mkv", new KodiMetadataConsumerSettings { MovieMetadataUrl = true });

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("Test Movie (2021).nfo");
		result[0].Content.ShouldContain("https://www.themoviedb.org/movie/200");
		result[0].Content.ShouldContain("https://www.imdb.com/title/tt200");
	}

	[Fact]
	public void Kodi_Images_ShouldUseStandardFileNames()
	{
		var consumer = new KodiMetadataConsumer();
		var seriesImages = consumer.SeriesImages(TestSeries, "Test Show", new KodiMetadataConsumerSettings());
		seriesImages.ShouldContain(x => x.RelativePath == "poster.jpg" && x.SourceUrl == TestSeries.PosterUrl);
		seriesImages.ShouldContain(x => x.RelativePath == "fanart.jpg" && x.SourceUrl == TestSeries.BackdropUrl);

		var episodeImages = consumer.EpisodeImages(TestSeries, [TestEpisode()], "Test Show - S01E02.mkv", "https://example.com/thumb.jpg", new KodiMetadataConsumerSettings());
		episodeImages.ShouldHaveSingleItem();
		episodeImages[0].RelativePath.ShouldBe("Test Show - S01E02-thumb.jpg");

		var seasonImages = consumer.SeasonImages(TestSeries, 1, null, new KodiMetadataConsumerSettings());
		seasonImages.ShouldHaveSingleItem();
		seasonImages[0].RelativePath.ShouldBe("season01-poster.jpg");
	}

	[Fact]
	public void Plex_SeriesMetadata_ShouldWritePlexMatchFile_WithEpisodeMappings()
	{
		var consumer = new PlexMetadataConsumer();
		var file = new EpisodeFile { RelativePath = "Season 01/Test Show - S01E02.mkv" };
		file.Episodes.Add(TestEpisode());

		var result = consumer.SeriesMetadata(TestSeries, [file], new PlexMetadataConsumerSettings { EpisodeMappings = true });

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe(".plexmatch");
		result[0].Content.ShouldContain("Title: Test Show");
		result[0].Content.ShouldContain("TvdbId: 100");
		result[0].Content.ShouldContain("Episode: S01E02: Season 01/Test Show - S01E02.mkv");
	}

	[Fact]
	public void Plex_ShouldNotSupportMoviesOrImages()
	{
		var consumer = new PlexMetadataConsumer();
		consumer.MovieMetadata(TestMovie, "movie.mkv", new PlexMetadataConsumerSettings()).ShouldBeEmpty();
		consumer.SeriesImages(TestSeries, "Test Show", new PlexMetadataConsumerSettings()).ShouldBeEmpty();
	}

	[Fact]
	public void Emby_MovieMetadata_ShouldWriteMovieXml_NextToFile()
	{
		var consumer = new EmbyMetadataConsumer();
		var result = consumer.MovieMetadata(TestMovie, "Movies/Test Movie (2021)/Test Movie.mkv", new EmbyMetadataConsumerSettings());

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe(Path.Combine("Movies/Test Movie (2021)", "movie.xml"));
		var doc = XDocument.Parse(result[0].Content);
		doc.Root!.Name.LocalName.ShouldBe("Movie");
		doc.Root.Element("LocalTitle")!.Value.ShouldBe("Test Movie");
	}

	[Fact]
	public void Emby_ShouldNotSupportSeries()
	{
		var consumer = new EmbyMetadataConsumer();
		consumer.SeriesMetadata(TestSeries, [], new EmbyMetadataConsumerSettings()).ShouldBeEmpty();
		consumer.EpisodeMetadata(TestSeries, [TestEpisode()], "x.mkv", new EmbyMetadataConsumerSettings()).ShouldBeEmpty();
	}

	[Fact]
	public void Roksbox_EpisodeMetadata_ShouldWriteVideoXml_WithXmlExtension()
	{
		var consumer = new RoksboxMetadataConsumer();
		var result = consumer.EpisodeMetadata(TestSeries, [TestEpisode()], "Season 01/Test Show - S01E02.mkv", new RoksboxMetadataConsumerSettings());

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("Season 01/Test Show - S01E02.xml");
		result[0].Content.ShouldContain("<video>");
		result[0].Content.ShouldContain("Test Show - 1x2 - Pilot");
	}

	[Fact]
	public void Roksbox_SeriesImage_ShouldBeNamedAfterSeriesFolder()
	{
		var consumer = new RoksboxMetadataConsumer();
		var result = consumer.SeriesImages(TestSeries, "Test Show", new RoksboxMetadataConsumerSettings());
		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("Test Show.jpg");
	}

	[Fact]
	public void Roksbox_MovieImages_ShouldNamePosterAndFanart_AfterFile()
	{
		var consumer = new RoksboxMetadataConsumer();
		var result = consumer.MovieImages(TestMovie, "Test Movie (2021).mkv", new RoksboxMetadataConsumerSettings());
		result.ShouldContain(x => x.RelativePath == "Test Movie (2021)-poster.jpg");
		result.ShouldContain(x => x.RelativePath == "Test Movie (2021)-fanart.jpg");
	}

	[Fact]
	public void Wdtv_EpisodeMetadata_ShouldUseWdtvFieldNames()
	{
		var consumer = new WdtvMetadataConsumer();
		var result = consumer.EpisodeMetadata(TestSeries, [TestEpisode()], "Season 01/Test Show - S01E02.mkv", new WdtvMetadataConsumerSettings());

		result.ShouldHaveSingleItem();
		result[0].RelativePath.ShouldBe("Season 01/Test Show - S01E02.xml");
		var doc = XDocument.Parse(result[0].Content);
		doc.Root!.Name.LocalName.ShouldBe("details");
		doc.Root.Element("series_name")!.Value.ShouldBe("Test Show");
		doc.Root.Element("season_number")!.Value.ShouldBe("01");
	}

	[Fact]
	public void Wdtv_Images_ShouldUseFolderJpgAndMetathumb()
	{
		var consumer = new WdtvMetadataConsumer();
		consumer.SeriesImages(TestSeries, "Test Show", new WdtvMetadataConsumerSettings()).ShouldContain(x => x.RelativePath == "folder.jpg");

		var episodeImages = consumer.EpisodeImages(TestSeries, [TestEpisode()], "Test Show - S01E02.mkv", "https://example.com/thumb.jpg", new WdtvMetadataConsumerSettings());
		episodeImages.ShouldHaveSingleItem();
		episodeImages[0].RelativePath.ShouldBe("Test Show - S01E02.metathumb");
	}

	[Fact]
	public void Wdtv_MovieMetadata_ShouldBeSkipped_WhenDisabled()
	{
		var consumer = new WdtvMetadataConsumer();
		consumer.MovieMetadata(TestMovie, "movie.mkv", new WdtvMetadataConsumerSettings { MovieMetadata = false }).ShouldBeEmpty();
	}
}
