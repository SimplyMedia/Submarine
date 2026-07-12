using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.MediaFile;
using Submarine.Core.MediaFile.Naming;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Core.Tests.MediaFile.Naming;

public class NamingTemplateRendererTest
{
	private readonly NamingTemplateRenderer _instance = new();

	private static SeriesNamingContext Series(
		IReadOnlyList<int> episodes,
		IReadOnlyList<string?> titles)
		=> new()
		{
			SeriesTitle = "The Series",
			SeasonNumber = 1,
			EpisodeNumbers = episodes,
			EpisodeTitles = titles
		};

	[Fact]
	public void Render_ShouldRenderStandardName_WhenSingleEpisode()
	{
		var result = _instance.Render(NamingDefaults.SeriesFileName,
			Series(new[] { 5 }, new[] { "The Title" }));

		Assert.Equal("The Series - S01E05 - The Title", result.Name);
		Assert.False(result.UsedPlaceholderTitle);
	}

	[Fact]
	public void Render_ShouldRenderRange_WhenMultiEpisodeContiguous()
	{
		var result = _instance.Render(NamingDefaults.SeriesFileName,
			Series(new[] { 1, 2, 3 }, new[] { "A", "B", "C" }));

		Assert.Equal("The Series - S01E01-E03 - A + B + C", result.Name);
	}

	[Fact]
	public void Render_ShouldConcatEpisodes_WhenMultiEpisodeNonContiguous()
	{
		var result = _instance.Render(NamingDefaults.SeriesFileName,
			Series(new[] { 1, 3 }, new[] { "A", "B" }));

		Assert.Equal("The Series - S01E01E03 - A + B", result.Name);
	}

	[Fact]
	public void Render_ShouldUseAbsoluteNumbering_WhenAnime()
	{
		var context = new SeriesNamingContext
		{
			SeriesTitle = "Some Anime",
			AbsoluteEpisodeNumbers = new[] { 15 },
			EpisodeTitles = new[] { "The Battle" },
			IsAnime = true
		};

		var result = _instance.Render(NamingDefaults.AnimeFileName, context);

		Assert.Equal("Some Anime - 015 - The Battle", result.Name);
	}

	[Fact]
	public void Render_ShouldSubstitutePlaceholder_WhenEpisodeTitleMissing()
	{
		var result = _instance.Render(NamingDefaults.SeriesFileName,
			Series(new[] { 5 }, new List<string?>()));

		Assert.Equal("The Series - S01E05 - Episode 5", result.Name);
		Assert.True(result.UsedPlaceholderTitle);
	}

	[Theory]
	[InlineData("Episode 5")]
	[InlineData("Ep. 5")]
	[InlineData("EP 5")]
	public void Render_ShouldFlagPlaceholder_WhenTitleLooksLikePlaceholder(string title)
	{
		var result = _instance.Render(NamingDefaults.SeriesFileName,
			Series(new[] { 5 }, new[] { title }));

		Assert.True(result.UsedPlaceholderTitle);
	}

	[Theory]
	[InlineData(false, false, false, "WebDL-1080p")]
	[InlineData(false, true, false, "WebDL-1080p Proper")]
	[InlineData(true, false, false, "WebDL-1080p Repack")]
	[InlineData(false, false, true, "WebDL-1080p REAL")]
	public void Render_ShouldRenderQualityFull_WhenRevisionPresent(
		bool isRepack, bool isProper, bool isReal, string expected)
	{
		var context = new SeriesNamingContext
		{
			QualityModel = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision(1, isRepack, isProper, isReal))
		};

		var result = _instance.Render("{Quality Full}", context);

		Assert.Equal(expected, result.Name);
	}

	[Fact]
	public void Render_ShouldRenderQualityTitle_WhenRevisionOmitted()
	{
		var context = new SeriesNamingContext
		{
			QualityModel = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision(2, false, true))
		};

		var result = _instance.Render("{Quality Title}", context);

		Assert.Equal("WebDL-1080p", result.Name);
	}

	[Theory]
	[InlineData("The Series-{Release Group}", null, "The Series")]
	[InlineData("The Series-{Release Group}", "RARBG", "The Series-RARBG")]
	[InlineData("The Series [{Release Group}]", null, "The Series")]
	[InlineData("The Series [{Release Group}]", "RARBG", "The Series [RARBG]")]
	public void Render_ShouldCleanReleaseGroup_WhenMissing(
		string template, string? group, string expected)
	{
		var context = new SeriesNamingContext { SeriesTitle = "The Series", ReleaseGroup = group };

		var result = _instance.Render(template, context);

		Assert.Equal(expected, result.Name);
	}

	[Fact]
	public void Render_ShouldUseDotSeparator_WhenTokenSpelledWithDots()
	{
		var context = new SeriesNamingContext
		{
			SeriesTitle = "The Series",
			SeasonNumber = 1,
			EpisodeNumbers = new[] { 5 }
		};

		var result = _instance.Render("{Series.Title}.S{Season:00}E{Episode:00}", context);

		Assert.Equal("The.Series.S01E05", result.Name);
	}

	[Fact]
	public void Render_ShouldSanitizeIllegalChars_WhenTitleContainsThem()
	{
		var context = new SeriesNamingContext { SeriesTitle = "Illegal<>Name." };

		var result = _instance.Render("{Series Title}", context);

		Assert.Equal("IllegalName", result.Name);
	}

	[Fact]
	public void Render_ShouldRenderCleanTitle_WhenCleanTitleToken()
	{
		var context = new SeriesNamingContext { SeriesTitle = "The Series: Origins!" };

		var result = _instance.Render("{Series CleanTitle}", context);

		Assert.Equal("The Series Origins", result.Name);
	}

	[Fact]
	public void Render_ShouldRenderEdition_WhenMoviePresent()
	{
		var context = new MovieNamingContext
		{
			MovieTitle = "Some Movie",
			Year = 2020,
			Edition = "Directors Cut"
		};

		var result = _instance.Render("{Movie Title} ({Year}) {Edition}", context);

		Assert.Equal("Some Movie (2020) Directors Cut", result.Name);
	}

	[Fact]
	public void Render_ShouldRenderMovieName_WhenDefaultTemplate()
	{
		var context = new MovieNamingContext { MovieTitle = "Some Movie", Year = 2020 };

		var result = _instance.Render(NamingDefaults.MovieFileName, context);

		Assert.Equal("Some Movie (2020)", result.Name);
	}

	[Theory]
	[InlineData(new[] { Language.ENGLISH }, "Some Movie")]
	[InlineData(new[] { Language.GERMAN }, "Some Movie German")]
	[InlineData(new[] { Language.GERMAN, Language.FRENCH }, "Some Movie German+French")]
	public void Render_ShouldJoinLanguages_AndOmitEnglishOnly(Language[] languages, string expected)
	{
		var context = new MovieNamingContext { MovieTitle = "Some Movie", Languages = languages };

		var result = _instance.Render("{Movie Title} {Languages}", context);

		Assert.Equal(expected, result.Name);
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND, "S01E01-02-03")]
	[InlineData(MultiEpisodeStyle.DUPLICATE, "S01E01.S01E02.S01E03")]
	[InlineData(MultiEpisodeStyle.REPEAT, "S01E01E02E03")]
	[InlineData(MultiEpisodeStyle.SCENE, "1x01x02x03")]
	[InlineData(MultiEpisodeStyle.RANGE, "S01E01-03")]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE, "S01E01-E03")]
	public void Render_ShouldRenderCluster_WhenMultiEpisodeContiguous(MultiEpisodeStyle style, string expected)
	{
		var context = new SeriesNamingContext { SeasonNumber = 1, EpisodeNumbers = new[] { 1, 2, 3 } };

		var result = _instance.Render("S{Season:00}E{Episode:00}", context, style);

		Assert.Equal(expected, result.Name);
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND)]
	[InlineData(MultiEpisodeStyle.RANGE)]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE)]
	public void Render_ShouldFallBackToRepeat_WhenMultiEpisodeNonContiguous(MultiEpisodeStyle style)
	{
		var context = new SeriesNamingContext { SeasonNumber = 1, EpisodeNumbers = new[] { 1, 3 } };

		var result = _instance.Render("S{Season:00}E{Episode:00}", context, style);

		Assert.Equal("S01E01E03", result.Name);
	}

	[Theory]
	[InlineData(MultiEpisodeStyle.EXTEND)]
	[InlineData(MultiEpisodeStyle.DUPLICATE)]
	[InlineData(MultiEpisodeStyle.REPEAT)]
	[InlineData(MultiEpisodeStyle.SCENE)]
	[InlineData(MultiEpisodeStyle.RANGE)]
	[InlineData(MultiEpisodeStyle.PREFIXED_RANGE)]
	public void Render_ShouldRenderPlainCluster_WhenSingleEpisode(MultiEpisodeStyle style)
	{
		var context = new SeriesNamingContext { SeasonNumber = 1, EpisodeNumbers = new[] { 1 } };

		var result = _instance.Render("S{Season:00}E{Episode:00}", context, style);

		Assert.Equal("S01E01", result.Name);
	}

	[Fact]
	public void Render_ShouldScopeSceneSeasonToCluster_WhenSceneStyle()
	{
		var context = new SeriesNamingContext { SeasonNumber = 1, EpisodeNumbers = new[] { 1, 2 } };

		var result = _instance.Render("Season {Season:00} - S{Season:00}E{Episode:00}", context, MultiEpisodeStyle.SCENE);

		Assert.Equal("Season 01 - 1x01x02", result.Name);
	}

	[Fact]
	public void Render_ShouldRenderMediaInfoTokens_WhenPresent()
	{
		var context = new SeriesNamingContext
		{
			MediaInfo = new MediaInfo
			{
				VideoCodec = "x265", AudioCodec = "dts", AudioChannels = 6, VideoDynamicRange = "HDR10"
			}
		};

		var result = _instance.Render(
			"{MediaInfo VideoCodec} {MediaInfo AudioCodec} {MediaInfo AudioChannels} {MediaInfo VideoDynamicRange}",
			context);

		Assert.Equal("x265 dts 5.1 HDR10", result.Name);
	}

	[Theory]
	[InlineData("The Series-{MediaInfo VideoCodec}", "The Series")]
	[InlineData("The Series [{MediaInfo VideoCodec}]", "The Series")]
	public void Render_ShouldCollapseMediaInfoToken_WhenAbsent(string template, string expected)
	{
		var context = new SeriesNamingContext { SeriesTitle = "The Series" };

		var result = _instance.Render(template, context);

		Assert.Equal(expected, result.Name);
	}

	[Theory]
	[InlineData(2.0, "2.0")]
	[InlineData(6.0, "5.1")]
	[InlineData(8.0, "7.1")]
	[InlineData(7.0, "7.0")]
	public void Render_ShouldFormatAudioChannels_PerChannelCount(double channels, string expected)
	{
		var context = new SeriesNamingContext { MediaInfo = new MediaInfo { AudioChannels = channels } };

		var result = _instance.Render("{MediaInfo AudioChannels}", context);

		Assert.Equal(expected, result.Name);
	}
}
