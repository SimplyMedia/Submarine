using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Naming;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Naming;

public class NamingServiceTest
{
	private readonly NamingService _instance = new();

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderStandardName_WhenSingleEpisode()
	{
		var result = _instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), File(),
			Config());

		result.ShouldBe("The Series - S01E05 - The Title");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("Episode 5")]
	[InlineData("Ep. 5")]
	[InlineData("EP 5")]
	public void UsesPlaceholderTitle_ShouldBeTrue_WhenEpisodeTitleIsMissingOrPlaceholder(string? title)
	{
		_instance.UsesPlaceholderTitle(StandardSeries(), Episodes(5, title), File(), Config()).ShouldBeTrue();
	}

	[Fact]
	public void UsesPlaceholderTitle_ShouldBeFalse_WhenEpisodeTitleIsKnown()
	{
		_instance.UsesPlaceholderTitle(StandardSeries(), Episodes(5, "The Title"), File(), Config()).ShouldBeFalse();
	}

	[Fact]
	public void UsesPlaceholderTitle_ShouldBeFalse_WhenTemplateHasNoEpisodeTitle()
	{
		var naming = Config();
		naming.StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}";

		_instance.UsesPlaceholderTitle(StandardSeries(), Episodes(5, null), File(), naming).ShouldBeFalse();
	}

	[Fact]
	public void UsesPlaceholderTitle_ShouldBeFalse_WhenRenamingIsDisabled()
	{
		var naming = Config();
		naming.RenameEpisodes = false;

		_instance.UsesPlaceholderTitle(StandardSeries(), Episodes(5, null), File(), naming).ShouldBeFalse();
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderRange_WhenMultiEpisodeContiguous()
	{
		var result = _instance.RenderEpisodeFileName(
			StandardSeries(),
			[Episode(1, "A"), Episode(2, "B"), Episode(3, "C")],
			File(),
			Config());

		result.ShouldBe("The Series - S01E01-E03 - A + B + C");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldConcatEpisodes_WhenMultiEpisodeNonContiguous()
	{
		var result = _instance.RenderEpisodeFileName(
			StandardSeries(),
			[Episode(1, "A"), Episode(3, "B")],
			File(),
			Config());

		result.ShouldBe("The Series - S01E01E03 - A + B");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldUseAbsoluteNumbering_WhenAnime()
	{
		var series = StandardSeries();
		series.Title = "Some Anime";
		series.Type = SeriesType.ANIME;

		var result = _instance.RenderEpisodeFileName(
			series,
			[AnimeEpisode(15, "The Battle")],
			File(),
			Config());

		result.ShouldBe("Some Anime - 015 - The Battle");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldSubstitutePlaceholder_WhenEpisodeTitleMissing()
	{
		var result = _instance.RenderEpisodeFileName(StandardSeries(), [Episode(5, null)], File(), Config());

		result.ShouldBe("The Series - S01E05 - Episode 5");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderAirDate_WhenDaily()
	{
		var series = StandardSeries();
		series.Type = SeriesType.DAILY;

		var config = Config();
		config.DailyEpisodeFormat = "{Series Title} - {Air Date} - {Episode Title}";

		var result = _instance.RenderEpisodeFileName(
			series,
			[DailyEpisode("2021-05-12")],
			File(),
			config);

		result.ShouldBe("The Series - 2021-05-12 - Episode 5");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderQualityFull_WhenRevisionPresent()
	{
		var file = File();

		var config = Config();
		config.StandardEpisodeFormat = "{Quality Full}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), file, config)
			.ShouldBe("WebDL-1080p");

		file.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
			new Revision(1, IsProper: true));

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), file, config)
			.ShouldBe("WebDL-1080p Proper");

		file.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
			new Revision(1, IsRepack: true));

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), file, config)
			.ShouldBe("WebDL-1080p Repack");

		file.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
			new Revision(1, IsReal: true));

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), file, config)
			.ShouldBe("WebDL-1080p REAL");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderQualityTitle_WhenRevisionOmitted()
	{
		var file = File();
		file.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
			new Revision(2, IsProper: true));

		var config = Config();
		config.StandardEpisodeFormat = "{Quality Title}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The Title"), file, config)
			.ShouldBe("WebDL-1080p");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderQualityProperAndRealTokens()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{Quality Proper} {Quality Real}";

		var proper = File();
		proper.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision(2, IsProper: true));

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), proper, config).ShouldBe("Proper");

		var real = File();
		real.Quality = new QualityModel(
			new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision(2, IsReal: true));

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), real, config).ShouldBe("REAL");
	}

	[Theory]
	[InlineData("The Series-{Release Group}", null, "The Series")]
	[InlineData("The Series-{Release Group}", "RARBG", "The Series-RARBG")]
	[InlineData("The Series [{Release Group}]", null, "The Series")]
	[InlineData("The Series [{Release Group}]", "RARBG", "The Series [RARBG]")]
	public void RenderEpisodeFileName_ShouldCleanReleaseGroup_WhenMissing(
		string template,
		string? group,
		string expected)
	{
		var file = File();
		file.ReleaseGroup = group;

		var config = Config();
		config.StandardEpisodeFormat = template;

		_instance.RenderEpisodeFileName(StandardSeries(), [Episode(5, "T")], file, config).ShouldBe(expected);
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldUseDotSeparator_WhenTokenSpelledWithDots()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{Series.Title}.S{Season:00}E{Episode:00}";

		var result = _instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), File(), config);

		result.ShouldBe("The.Series.S01E05");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldApplyCaseModifiers()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{SERIES TITLE} {series title} {Series.Title}";

		var result = _instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), File(), config);

		result.ShouldBe("THE SERIES the series The.Series");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldSanitizeIllegalChars_WhenTitleContainsThem()
	{
		var series = StandardSeries();
		series.Title = "Illegal<>Name.";

		var config = Config();
		config.StandardEpisodeFormat = "{Series Title}";

		_instance.RenderEpisodeFileName(series, Episodes(5, "T"), File(), config).ShouldBe("IllegalName");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldKeepIllegalChars_WhenReplacementIsDisabled()
	{
		var series = StandardSeries();
		series.Title = "Illegal<Name>";

		var config = Config();
		config.ReplaceIllegalCharacters = false;
		config.StandardEpisodeFormat = "{Series Title}";

		_instance.RenderEpisodeFileName(series, Episodes(5, "T"), File(), config).ShouldBe("Illegal<Name>");
	}

	[Theory]
	[InlineData(ColonReplacement.DELETE, "A B")]
	[InlineData(ColonReplacement.DASH, "A- B")]
	[InlineData(ColonReplacement.SPACE_DASH, "A - B")]
	[InlineData(ColonReplacement.SPACE_DASH_SPACE, "A - B")]
	[InlineData(ColonReplacement.SMART, "A - B")]
	public void RenderEpisodeFileName_ShouldApplyColonReplacement(ColonReplacement mode, string expected)
	{
		var series = StandardSeries();
		series.Title = "A: B";

		var config = Config();
		config.ReplaceIllegalCharacters = false;
		config.ColonReplacement = mode;
		config.StandardEpisodeFormat = "{Series Title}";

		_instance.RenderEpisodeFileName(series, Episodes(5, "T"), File(), config).ShouldBe(expected);
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderCleanTitle_WhenCleanTitleToken()
	{
		var series = StandardSeries();
		series.Title = "The Series: Origins!";

		var config = Config();
		config.StandardEpisodeFormat = "{Series CleanTitle}";

		_instance.RenderEpisodeFileName(series, Episodes(5, "T"), File(), config)
			.ShouldBe("The Series Origins");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderTitleTheAndTitleYear()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{Series TitleThe} ({Series TitleYear})";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), File(), config)
			.ShouldBe("Series, The (The Series 2010)");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderEpisodeCleanTitle()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{Episode CleanTitle}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "The: Title!"), File(), config)
			.ShouldBe("The Title");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderIdentifierTokens()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{TvdbId} {TmdbId} {ImdbId}";

		var result = _instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), File(), config);

		result.ShouldBe("123456 654321 tt0944947");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderCustomFormats()
	{
		var config = Config();
		config.StandardEpisodeFormat = "{Custom Formats} {Custom Format:HD}";

		var result = _instance.RenderEpisodeFileName(
			StandardSeries(), Episodes(5, "T"), File(), config, ["HD", "Proper"]);

		result.ShouldBe("HD+Proper HD");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldCollapseCustomFormatToken_WhenFormatIsAbsent()
	{
		var config = Config();
		config.StandardEpisodeFormat = "The Series {Custom Format:Missing}";

		var result = _instance.RenderEpisodeFileName(
			StandardSeries(), Episodes(5, "T"), File(), config, ["HD"]);

		result.ShouldBe("The Series");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderOriginalTitleAndFilename()
	{
		var file = File();
		file.SceneName = "Series.Title.S01E05.1080p-GROUP";
		file.RelativePath = "/downloads/Series.Title.S01E05.1080p-GROUP.mkv";

		var config = Config();
		config.StandardEpisodeFormat = "{Original Title} {Original Filename}";

		var result = _instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), file, config);

		result.ShouldBe("Series.Title.S01E05.1080p-GROUP Series.Title.S01E05.1080p-GROUP.mkv");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderMediaInfoTokens_WhenPresent()
	{
		var file = File();
		file.MediaInfo = new MediaInfoModel("x265", "dts", 6, "HDR10", 3840, 2160, 42, VideoBitDepth: 10);

		var config = Config();
		config.StandardEpisodeFormat =
			"{MediaInfo Simple} {MediaInfo Full} {MediaInfo VideoBitDepth}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), file, config)
			.ShouldBe("x265 dts x265 dts 5.1 HDR10 10");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldRenderMediaInfoLanguageTokens()
	{
		var file = File();
		file.MediaInfo = new MediaInfoModel(
			"x264", "aac", 2, "SDR", 1920, 1080, 42,
			AudioLanguages: ["English", "German"], SubtitleLanguages: ["French"]);

		var config = Config();
		config.StandardEpisodeFormat = "{MediaInfo AudioLanguages} {MediaInfo SubtitleLanguages}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), file, config)
			.ShouldBe("English+German French");
	}

	[Theory]
	[InlineData("The Series-{MediaInfo VideoCodec}", "The Series")]
	[InlineData("The Series [{MediaInfo VideoCodec}]", "The Series")]
	public void RenderEpisodeFileName_ShouldCollapseMediaInfoToken_WhenAbsent(string template, string expected)
	{
		var config = Config();
		config.StandardEpisodeFormat = template;

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), File(), config)
			.ShouldBe(expected);
	}

	[Theory]
	[InlineData(2.0, "2.0")]
	[InlineData(6.0, "5.1")]
	[InlineData(8.0, "7.1")]
	[InlineData(7.0, "7.0")]
	public void RenderEpisodeFileName_ShouldFormatAudioChannels_PerChannelCount(double channels, string expected)
	{
		var file = File();
		file.MediaInfo = new MediaInfoModel(null, null, channels, null, null, null, null);

		var config = Config();
		config.StandardEpisodeFormat = "{MediaInfo AudioChannels}";

		_instance.RenderEpisodeFileName(StandardSeries(), Episodes(5, "T"), file, config).ShouldBe(expected);
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND, "S01E01-02-03")]
	[InlineData(MultiEpisodeStyle.DUPLICATE, "S01E01.S01E02.S01E03")]
	[InlineData(MultiEpisodeStyle.REPEAT, "S01E01E02E03")]
	[InlineData(MultiEpisodeStyle.SCENE, "1x01x02x03")]
	[InlineData(MultiEpisodeStyle.RANGE, "S01E01-03")]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE, "S01E01-E03")]
	public void RenderEpisodeFileName_ShouldRenderCluster_WhenMultiEpisodeContiguous(
		MultiEpisodeStyle style,
		string expected)
	{
		var config = Config();
		config.StandardEpisodeFormat = "S{Season:00}E{Episode:00}";
		config.MultiEpisodeStyle = style;

		var result = _instance.RenderEpisodeFileName(
			StandardSeries(), [Episode(1, "A"), Episode(2, "B"), Episode(3, "C")], File(), config);

		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND)]
	[InlineData(MultiEpisodeStyle.RANGE)]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE)]
	public void RenderEpisodeFileName_ShouldFallBackToRepeat_WhenMultiEpisodeNonContiguous(MultiEpisodeStyle style)
	{
		var config = Config();
		config.StandardEpisodeFormat = "S{Season:00}E{Episode:00}";
		config.MultiEpisodeStyle = style;

		var result = _instance.RenderEpisodeFileName(
			StandardSeries(), [Episode(1, "A"), Episode(3, "B")], File(), config);

		result.ShouldBe("S01E01E03");
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND)]
	[InlineData(MultiEpisodeStyle.DUPLICATE)]
	[InlineData(MultiEpisodeStyle.REPEAT)]
	[InlineData(MultiEpisodeStyle.SCENE)]
	[InlineData(MultiEpisodeStyle.RANGE)]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE)]
	public void RenderEpisodeFileName_ShouldRenderPlainCluster_WhenSingleEpisode(MultiEpisodeStyle style)
	{
		var config = Config();
		config.StandardEpisodeFormat = "S{Season:00}E{Episode:00}";
		config.MultiEpisodeStyle = style;

		var result = _instance.RenderEpisodeFileName(StandardSeries(), [Episode(1, "A")], File(), config);

		result.ShouldBe("S01E01");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldScopeSceneSeasonToCluster_WhenSceneStyle()
	{
		var config = Config();
		config.StandardEpisodeFormat = "Season {Season:00} - S{Season:00}E{Episode:00}";
		config.MultiEpisodeStyle = MultiEpisodeStyle.SCENE;

		var result = _instance.RenderEpisodeFileName(
			StandardSeries(), [Episode(1, "A"), Episode(2, "B")], File(), config);

		result.ShouldBe("Season 01 - 1x01x02");
	}

	[Fact]
	public void RenderEpisodeFileName_ShouldTrimLongNames()
	{
		var series = StandardSeries();
		series.Title = new string('x', 300);

		var config = Config();
		config.StandardEpisodeFormat = "{Series Title}";

		_instance.RenderEpisodeFileName(series, Episodes(5, "T"), File(), config).Length.ShouldBe(255);
	}

	[Fact]
	public void RenderMovieFileName_ShouldRenderEdition_WhenMoviePresent()
	{
		var config = Config();
		config.MovieFormat = "{Movie Title} ({Year}) {Edition Tags}";

		var file = MovieFile();
		file.Edition = "Directors Cut";

		_instance.RenderMovieFileName(Movie(), file, config).ShouldBe("Some Movie (2020) Directors Cut");
	}

	[Fact]
	public void RenderMovieFileName_ShouldRenderMovieName_WhenDefaultTemplate()
	{
		_instance.RenderMovieFileName(Movie(), MovieFile(), Config()).ShouldBe("Some Movie (2020)");
	}

	[Theory]
	[InlineData(new[] { 0 }, "Some Movie")]
	[InlineData(new[] { 1 }, "Some Movie German")]
	[InlineData(new[] { 1, 2 }, "Some Movie German+French")]
	public void RenderMovieFileName_ShouldJoinLanguages_AndOmitEnglishOnly(int[] languageIndexes, string expected)
	{
		var allLanguages = new[] { Language.ENGLISH, Language.GERMAN, Language.FRENCH };
		var languages = languageIndexes.Select(index => allLanguages[index]).ToList();

		var config = Config();
		config.MovieFormat = "{Movie Title} {Languages}";

		_instance.RenderMovieFileName(Movie(), MovieFile(languages), config).ShouldBe(expected);
	}

	[Fact]
	public void RenderSeriesFolder_ShouldRenderTheSeriesTitle()
	{
		_instance.RenderSeriesFolder(Config(), StandardSeries()).ShouldBe("The Series");
	}

	[Fact]
	public void RenderSeasonFolder_ShouldRenderTheSeasonNumber()
	{
		_instance.RenderSeasonFolder(Config(), StandardSeries(), 2).ShouldBe("Season 02");
	}

	[Fact]
	public void RenderSeasonFolder_ShouldRenderTheSpecialsFolder_WhenSeasonIsZero()
	{
		_instance.RenderSeasonFolder(Config(), StandardSeries(), 0).ShouldBe("Specials");
	}

	[Fact]
	public void RenderMovieFolder_ShouldRenderTheMovieFolder()
	{
		_instance.RenderMovieFolder(Config(), Movie()).ShouldBe("Some Movie (2020)");
	}

	private static Series StandardSeries()
		=> new()
		{
			Title = "The Series",
			Year = 2010,
			TvdbId = 123456,
			TmdbId = 654321,
			ImdbId = "tt0944947",
			Type = SeriesType.STANDARD
		};

	private static Episode[] Episodes(int number, string? title)
		=> [Episode(number, title)];

	private static Episode Episode(int number, string? title)
		=> new() { SeasonNumber = 1, EpisodeNumber = number, Title = title };

	private static Episode AnimeEpisode(int absoluteNumber, string? title)
		=> new() { SeasonNumber = 1, EpisodeNumber = absoluteNumber, AbsoluteEpisodeNumber = absoluteNumber, Title = title };

	private static Episode DailyEpisode(string airDate)
		=> new() { SeasonNumber = 1, EpisodeNumber = 5, Title = "Episode 5", AirDate = airDate };

	private static EpisodeFile File()
		=> new()
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Languages = [Language.ENGLISH],
			ReleaseGroup = null,
			RelativePath = "Series.Title.S01E05.1080p-GROUP.mkv"
		};

	private static Movie Movie()
		=> new() { Title = "Some Movie", Year = 2020, TmdbId = 42 };

	private static MovieFile MovieFile(List<Language>? languages = null)
		=> new()
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R2160_P),
				new Revision()),
			Languages = languages ?? [Language.ENGLISH],
			RelativePath = "Movie.Title.2020.2160p-GROUP.mkv"
		};

	private static NamingConfig Config()
		=> new();
}
