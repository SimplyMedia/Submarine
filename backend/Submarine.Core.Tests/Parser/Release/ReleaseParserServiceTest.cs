using System;
using System.Collections.Generic;
using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Xunit;

namespace Submarine.Core.Tests.Parser.Release;

public class ReleaseParserServiceTest
{
	private readonly IParser<BaseRelease> _instance;

	private readonly ITestOutputHelper _output;

	public ReleaseParserServiceTest(ITestOutputHelper output)
	{
		_output = output;
		_instance = CreateInstance(output, new StaticQualityOverrideSource());
	}

	private static ReleaseParserService CreateInstance(ITestOutputHelper output, IQualityOverrideSource overrideSource)
		=> new(new XunitLogger<ReleaseParserService>(output),
			new LanguageParserService(new XunitLogger<LanguageParserService>(output)),
			new StreamingProviderParserService(new XunitLogger<StreamingProviderParserService>(output)),
			new QualityParserService(new XunitLogger<QualityParserService>(output)),
			new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output)),
			overrideSource);


	[Theory]
	[InlineData("Anime Show AKA Japanese Name S01 1080p WEB-DL Dual-Audio AAC 2.0 H.264-ZR", "Japanese Name",
		new[] { "Anime Show" })]
	[InlineData("Wheel of Poverty and Reality AKA Other Language Title 2021 1080p BluRay REMUX AVC DTS-HD MA 5.1-C0M3T",
		"Other Language Title", new[] { "Wheel of Poverty and Reality" })]
	[InlineData("Title One AKA Title Two AKA Title Three AKA Title Four S04 1080p WEB-DL AAC 2.0 H.264-NOGROUP",
		"Title Four", new[] { "Title One", "Title Two", "Title Three" })]
	[InlineData("Title.Someone.Returns.AKA.Title.Someone.Returns.2010.DVDRip.x264-HANDJOB-BUYMORE",
		"Title Someone Returns", new[] { "Title Someone Returns" })]
	public void Parse_ShouldParseAliases_WhenTitleContainsAKA(string input, string title, string[] aliases)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(input, parsed.FullTitle);
		Assert.Equal(title, parsed.Title);
		Assert.Equal(aliases, parsed.Aliases);
	}

	[Theory]
	[InlineData("Show.Name - S01E01 1080p WEB-DL x264-GROUP", "Show Name")]
	[InlineData("[ www.Torrenting.com ] - Show.Name.S01E01.1080p.WEB-DL.x264-GROUP", "Show Name")]
	[InlineData("www.tamilblasters.rest - Show Name S01E01 1080p WEB-DL x264-GROUP", "Show Name")]
	public void Parse_ShouldParseTitle_WhenWebsiteLikePrefixIsPresent(string input, string title)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(title, parsed.Title);
	}

	[Theory]
	[InlineData("Anime S01 2021 1080p WEB-DL AVC AAC 2.0 Dual Audio -ZR-", "Anime")]
	[InlineData("The Anime Title (Japanese Alias) S01 2021 1080p WEB-DL AVC AAC 2.0 Dual Audio -ZR-",
		"The Anime Title")]
	[InlineData("The Anime Title (Japanese Alias) S04E18 2022 1080p WEB-DL AVC AAC 2.0 Dual Audio -ZR-",
		"The Anime Title")]
	public void Parse_ShouldParseTitle_WhenReleaseIsAnime(string input, string title)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(title, parsed.Title);
	}

	[Theory]
	[InlineData("Series S05 720p BluRay DTS 5.1 x264-DEMAND")]
	[InlineData("The Series AKA The Alias 2020 S01 1080p WEB-DL DD 5.1 H.264-NTb")]
	[InlineData("Some.French.Series.Name.S01E01.Parte.01.1080p.HMAX.WEB-DL.DD2.0.x264-alfaHD")]
	[InlineData("Long.Anime.S13E1037.1080p.WEB.H264-SENPAI")]
	public void Parse_ShouldIdentifySeries_WhenReleaseIsSeries(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.SERIES, parsed.Type);
	}

	[Theory]
	[InlineData("Movie Name AKA Other Movie Name 1983 1080p BluRay REMUX AVC FLAC 2.0-BLURANiUM")]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC")]
	[InlineData("Movie.Title.1989.MULTi.DV.2160p.UHD.BluRay.x265-SESKAPiLE")]
	[InlineData("Movie.2019.MULTi.COMPLETE.UHD.BLURAY-PRECELL")]
	public void Parse_ShouldIdentifyMovie_WhenReleaseIsMovie(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.MOVIE, parsed.Type);
	}

	[Theory]
	[InlineData("[HatSubs] Anime Title 1004 [E63F2984].mkv", 1004)]
	public void Parse_ShouldParseAbsoluteEpisode_WhenReleaseIsAnime(string input, int absoluteEpisode)
	{
		var parsed = _instance.Parse(input);

		Assert.Contains(absoluteEpisode,
			parsed.SeriesReleaseData?.AbsoluteEpisodes ?? throw new InvalidOperationException());
	}

	[Theory]
	[InlineData("[HorribleSubs] Anime - 12 [1080p].mkv", QualitySource.WEB_DL)]
	[InlineData("[SubsPlease] Anime - 14 (1080p) [3168B4D7].mkv", QualitySource.WEB_DL)]
	[InlineData(
		"[Erai-raws] Anime 2nd Season - 11 [1080p][Multiple Subtitle] [ENG][POR-BR][SPA-LA][SPA][GER][ITA][RUS]",
		QualitySource.WEB_DL)]
	public void Parse_ShouldApplyEdgeCaseReleaseGroupQualitySourceMapping_WhenReleaseHasUnknownSourceAndGroupMatches(
		string input, QualitySource expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Quality.Resolution.Source);
	}

	[Theory]
	[InlineData("[Erai-raws] Anime - 01 ~ 24 [BD 720p][Multiple Subtitle]", QualitySource.BLURAY)]
	public void Parse_ShouldNotApplyEdgeCaseReleaseGroupQualitySourceMapping_IfReleaseSpecifiesQualitySource(
		string input, QualitySource expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Quality.Resolution.Source);
	}

	[Theory]
	[InlineData("[CustomGroup] Anime - 12 [1080p].mkv", "CustomGroup", QualitySource.BLURAY)]
	[InlineData("[AnotherGroup] Anime - 03 (720p) [ABCD1234].mkv", "anothergroup", QualitySource.WEB_RIP)]
	public void Parse_ShouldApplyConfiguredQualityOverride_WhenReleaseHasNoExplicitQuality(string input, string group,
		QualitySource expected)
	{
		var instance = CreateInstance(_output, new StaticQualityOverrideSource(
			new Dictionary<string, QualitySource>(StringComparer.OrdinalIgnoreCase) { { group, expected } }));

		var parsed = instance.Parse(input);

		Assert.Equal(expected, parsed.Quality.Resolution.Source);
	}

	[Theory]
	[InlineData("Movie.Title.Directors.Cut.2011.1080p.BluRay.x264-GROUP", "Directors Cut")]
	[InlineData("Movie.Title.Director's.Cut.2011.1080p.BluRay.x264-GROUP", "Director's Cut")]
	public void Parse_ShouldParseEdition_WhenMovieHasDirectorsCut(string input, string edition)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.MOVIE, parsed.Type);
		Assert.Equal(edition, parsed.MovieReleaseData?.Edition);
	}

	[Theory]
	[InlineData("Movie.Title.Extended.2011.1080p.BluRay.x264-GROUP", "Extended")]
	[InlineData("Movie.Title.Theatrical.2011.1080p.BluRay.x264-GROUP", "Theatrical")]
	[InlineData("Movie.Title.IMAX.2011.1080p.BluRay.x264-GROUP", "IMAX")]
	[InlineData("Movie.Title.Remastered.2011.1080p.BluRay.x264-GROUP", "Remastered")]
	[InlineData("Movie.Title.Uncut.2011.1080p.BluRay.x264-GROUP", "Uncut")]
	[InlineData("Movie.Title.Unrated.2011.1080p.BluRay.x264-GROUP", "Unrated")]
	[InlineData("Movie.Title.Special.Edition.2011.1080p.BluRay.x264-GROUP", "Special Edition")]
	[InlineData("Movie.Title.Limited.2011.1080p.BluRay.x264-GROUP", "Limited")]
	[InlineData("Movie.Title.Criterion.2011.1080p.BluRay.x264-GROUP", "Criterion")]
	[InlineData("Movie.Title.25th.Anniversary.Edition.2011.1080p.BluRay.x264-GROUP", "25th Anniversary Edition")]
	[InlineData("Movie.Title.3D.2011.1080p.BluRay.x264-GROUP", "3D")]
	public void Parse_ShouldParseEdition_WhenMovieHasEdition(string input, string edition)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.MOVIE, parsed.Type);
		Assert.Equal(edition, parsed.MovieReleaseData?.Edition);
	}

	[Theory]
	[InlineData("Movie.Title.1987.1080p.BluRay.REMUX.DD+2.0.AVC")]
	public void Parse_ShouldSetMovieReleaseDataWithoutEdition_WhenMovieHasNoEdition(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.MOVIE, parsed.Type);
		Assert.NotNull(parsed.MovieReleaseData);
		Assert.Null(parsed.MovieReleaseData?.Edition);
	}

	[Theory]
	// Skymoon-Raws / orion origin normalization (PreSubstitutionRegex)
	[InlineData("[Skymoon-Raws]Muv-Luv Alternative - 05[WebRip 1080p HEVC].mkv", "Muv-Luv Alternative")]
	[InlineData("[orion origin]Some Anime Title - 07[1080p].mkv", "Some Anime Title")]
	// GM-Team normalization (PreSubstitutionRegex)
	[InlineData("[GM-Team][国漫][Sword Title][2021][05][1080p].mkv", "Sword Title")]
	public void Parse_ShouldNormalizeChineseSubGroups_WhenReleaseUsesPreSubstitution(string input, string title)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.SERIES, parsed.Type);
		Assert.Equal(title, parsed.Title);
	}

	[Theory]
	// Japanese variety shows with leading date
	[InlineData("230101 Variety Show ep05 1080p WEB-DL", "Variety Show")]
	// Turkish tracker releases (BLM/Bölüm)
	[InlineData("Dizi Adi 05 Bolum 1080p WEB-DL x264", "Dizi Adi")]
	// Dutch/Flemish release titles
	[InlineData("Programma Se.02 afl.05 1080p WEB-DL", "Programma")]
	public void Parse_ShouldParseLocalizedSeries_WhenReleaseUsesNewTitleBankPattern(string input, string title)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.SERIES, parsed.Type);
		Assert.Equal(title, parsed.Title);
	}

	[Theory]
	// Spanish tracker releases (Temporada/Cap)
	[InlineData("La Casa Temporada 1 [Cap.105] 1080p WEB-DL")]
	public void Parse_ShouldIdentifySeries_WhenReleaseUsesSpanishTrackerPattern(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.SERIES, parsed.Type);
	}

	[Theory]
	// Anime - Title Episode/Episodio Absolute Episode Number
	[InlineData("[SubGroup] Anime Name Episodio 12 [1080p]", 12)]
	// Anime - Title [Absolute Episode Number] from AniLibriaTV
	[InlineData("Anime Title [12] 1080p BDRip", 12)]
	public void Parse_ShouldParseAbsoluteEpisode_WhenReleaseUsesNewAnimeTitleBankPattern(string input,
		int absoluteEpisode)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(ReleaseType.SERIES, parsed.Type);
		Assert.Contains(absoluteEpisode,
			parsed.SeriesReleaseData?.AbsoluteEpisodes ?? throw new InvalidOperationException());
	}
}
