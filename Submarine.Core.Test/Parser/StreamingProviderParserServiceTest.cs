using Submarine.Core.Parser;
using Submarine.Core.Quality;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Parser;

public class StreamingProviderParserServiceTest
{
	private readonly IParser<StreamingProvider?> _instance;

	public StreamingProviderParserServiceTest(ITestOutputHelper output)
		=> _instance = new StreamingProviderParserService(new XunitLogger<StreamingProviderParserService>(output));

	[Theory]
	[InlineData("Movie 2021 1080p AMZN WEB-DL H 264 DDP5.1-DRG")]
	[InlineData("Movie Title 2021 1080p AMZN WEBRip DD2 0 X 264-EVO")]
	[InlineData("The.Series.Title.S01E01.Part.1.720p.AMZN.WEB-DL.DDP5.1.H.264-NTG")]
	[InlineData("A Movie Title 2006 Amazon WEB-DL DD+2.0 H.264-QOQ")]
	[InlineData("Show Title S01E01 Home Is Where The Bark Is 1080p Amazon WEB DL DD 2 0 H 264 QOQ")]
	[InlineData("Long.Running.Documentary.Show.S01E01.Amazon.WEB.x264-nonspin")]
	public void Parse_ShouldReturnStreamingProviderAmazon_WhenReleaseIsAmazon(string input)
		=> AssertStreamingProvider(input, StreamingProvider.AMAZON);

	[Theory]
	[InlineData("Show.Title.S01E37.1080p.NF.WEB-DL.DDP2.0.x264-Ao.mkv")]
	[InlineData("The Perfect Application S01 1080p NF WEB-DL DDP5.1 H264-NTb")]
	[InlineData("Show.Title.S01E01.1080p.NF.WEB-DL.DDP5.1.DV.HEVC-FLUX")]
	[InlineData("Series Title S01 1080p Netflix WEB-DL DD5 1 x264-TrollHD")]
	public void Parse_ShouldReturnStreamingProviderNetflix_WhenReleaseIsNetflix(string input)
		=> AssertStreamingProvider(input, StreamingProvider.NETFLIX);

	[Theory]
	[InlineData("Movie 2021 1080p ATVP WEBRip X 264 AC3-EVO")]
	[InlineData("Show.Title.Long.2017.720p.ATVP.WEB-DL.DDP5.1.Atmos.H.264-TEPES")]
	[InlineData("The.Series.S01.1080p.ATVP.WEB-DL.DDP5.1.H.264-CasStudio")]
	[InlineData("Movie.2160p.APTV.WEB-DL.DDP5.1.Atmos.HEVC-CMRG")]
	[InlineData("Movie Title 2021 2160p APTV WEB-DL DD+5.1 HEVC-EVO")]
	[InlineData("Daily Show Title 2019 S01E01 1080p PROPER APTV WEB DL DD5 1 H 264 1 MZABI Obfuscated")]
	public void Parse_ShouldReturnStreamingProviderAppleTV_WhenReleaseIsAppleTV(string input)
		=> AssertStreamingProvider(input, StreamingProvider.APPLE_TV);

	[Theory]
	[InlineData("The Really Long Series Title S02 1080p HMAX WEB-DL DD 2.0 H264-ViSiON")]
	[InlineData("Show.Title.S03E13.Debut.1080p.HMAX.WEB-DL.DD2.0.H.264-FLUX")]
	[InlineData("Title S01 1080p HMAX WEB-DL DD5 1 H 264-NTb")]
	[InlineData("Movie Title 2021 1080 Hmax Webdl X264 Ac3 Will1869")]
	public void Parse_ShouldReturnStreamingProviderHBOMax_WhenReleaseIsHBOMax(string input)
		=> AssertStreamingProvider(input, StreamingProvider.HBO_MAX);

	[Theory]
	[InlineData("Show.S03E15.DSNP.WEB-DL.AAC2.0.H.264-BTN")]
	[InlineData("Show Title S01 1080p DSNP WEB-DL DD+ 5.1 Atmos H.264-FLUX")]
	[InlineData("Show.Title.S02E08.So-fish-ticated-Milo.And.Oscar.Move.In.720p.DSNY.WEBRip.AAC2.0.X264-TVSmash")]
	[InlineData("Series Title S02E03 (The Vibe) 720p DSNY WEB X264 Solar")]
	[InlineData("Movie title Extras 720p Disney+ WEB-DL AAC2 0 x 264 Kylo")]
	[InlineData("Movie.Title.2021.1080p.Disney.WebDL.H264.AC3.Will1869")]
	[InlineData("Series.Title.S16.DP.WEB.720p.DDP.5.1.H.264.PLEX")]
	public void Parse_ShouldReturnStreamingProviderDisney_WhenReleaseIsDisney(string input)
		=> AssertStreamingProvider(input, StreamingProvider.DISNEY);

	[Theory]
	[InlineData("Show Title S01 720p HULU WEBRip AAC2 0 H.264-RTN")]
	[InlineData("Title S01E06 HULU WEB-DL 720p-WhiteHat")]
	[InlineData("A Show S01 1080p HULU WEB-DL x264 DDP 5.1-TOMMY")]
	[InlineData("Show Title S01E01 1080p HULU WEB-DL DD+ 5.1 H.264-LAZY")]
	public void Parse_ShouldReturnStreamingProviderHulu_WhenReleaseIsHulu(string input)
		=> AssertStreamingProvider(input, StreamingProvider.HULU);

	[Theory]
	[InlineData("Anime Title - 09 VOSTFR 1080p WEB x264 -NanDesuKa (CR).mkv")]
	[InlineData("Anime Title - 11 VOSTFR 720p WEB x264 -NanDesuKa (CR).mkv")]
	[InlineData(
		"[Golumpa] Anime Title S2 - 03 (100-man no Inochi no Ue ni Ore wa Tatteiru S2) [English Dub] [CR-Dub 1080p x264 AAC] [MKV] [C229250A]")]
	[InlineData("Anime Title!! - 07 (Crunchyroll 1080p)")]
	[InlineData("Anime Title AKA Alias Title S04 1080p CR WEB-DL Dual-Audio AAC 2.0 H.264-ZR")]
	public void Parse_ShouldReturnStreamingProviderCrunchyroll_WhenReleaseIsCrunchyroll(string input)
		=> AssertStreamingProvider(input, StreamingProvider.CRUNCHYROLL);

	[Theory]
	[InlineData(
		"[Golumpa] Anime Title - The Ouka Ninja Scrolls - 17 (Basilisk - Ouka Ninpouchou) [English Dub] [FuniDub 720p x264 AAC] [MKV] [22327EE2]")]
	[InlineData("Anime Title - 05 - On Love-Spring Edition (1080p FUNI WEB-DL -KS-)")]
	[InlineData("Kinda long Anime Title S01E01 North Gate Smoke Tower (1080p FUNI WEB DL KS )")]
	[InlineData("[-KS-] Anime Title (2020) - 01-02 [720p] [Dual Audio] [FUNimation]")]
	[InlineData("[Funimation.DUB].Anime.Title.Uncommon.-.51.[1080p][EXIL3]")]
	public void Parse_ShouldReturnStreamingProviderFunimation_WhenReleaseIsFunimation(string input)
		=> AssertStreamingProvider(input, StreamingProvider.FUNIMATION);

	[Theory]
	[InlineData("Title.S01E02.2160p.RED.WEB-DL.AAC5.1.VP9-NTb")]
	[InlineData("Title.S01E07.1080p.RED.WEB-DL.AAC5.1.VP9-NTb")]
	[InlineData("Title.S02E02.Fight.or.Flight.1080p.RED.WEB-DL.AAC5.1.VP9-AJP69")]
	[InlineData("The.Title.S01E05.1080p.RED.WEBRip.AAC5.1.VP9-BTW-Obfuscated")]
	public void Parse_ShouldReturnStreamingProviderYoutubePremium_WhenReleaseIsYoutubePremium(string input)
		=> AssertStreamingProvider(input, StreamingProvider.YOUTUBE_PREMIUM);

	[Theory]
	[InlineData("The Title S03 1080p PCOK WEB-DL DDP5 1 x264-NTb")]
	[InlineData("The Title & More 1998 1080p PCOK WEB-DL DD+ 5.1 H.264-monkee")]
	[InlineData("The.Show.US.S01E04.The.Alliance.Extended.Cut.1080p.PCOK.WEB-DL.DDP5.1.H.264-TEPES")]
	public void Parse_ShouldReturnStreamingProviderPeacock_WhenReleaseIsPeacock(string input)
		=> AssertStreamingProvider(input, StreamingProvider.PEACOCK);

	[Theory]
	[InlineData("Title.2018.S02E02.Rose.720p.DCU.WEB-DL.DD5.1.H264-NTb")]
	[InlineData("Show.Title.S03E26.1080p.DCU.WEB-DL.AAC2.0.H.264.1-NTb")]
	[InlineData("Show S01E02 S T R I P E (1080p DCU Webrip x265 10bit EAC3 5 1 - Goki)[TAoE]")]
	[InlineData("Series.S01E08.The.Boy.Who.Said.'No'.1080p.DCU.WEB-DL.AAC2.0.H.264-EMb")]
	public void Parse_ShouldReturnStreamingProviderDCUniverse_WhenReleaseIsDCUniverse(string input)
		=> AssertStreamingProvider(input, StreamingProvider.DC_UNIVERSE);

	[Theory]
	[InlineData("Series.Title.2019.1080p.HBO.WEB-DL.AAC2.0.H.264-playWEB")]
	[InlineData("Show.Title.S01E03.1080p.HBO.WEB-DL.H264-PPSh")]
	[InlineData("The Show S47E14 Painting with Cookie Monster 1080p HBO WEBRip DD2 0 H 264-VLAD")]
	[InlineData("Show.Title.S04E04.Teambuilding.Exercise.720p.HBO.WEB-DL.DD5.1.H.264-monkee")]
	public void Parse_ShouldReturnStreamingProviderHBONow_WhenReleaseIsHBONow(string input)
		=> AssertStreamingProvider(input, StreamingProvider.HBO_NOW);

	[Theory]
	[InlineData("Movie 2021 1080p WEB-DL DD5 1 H 264-EVO")]
	[InlineData("Movie 2021 1080p WEB-DL DD5 1 H 264-CMRG")]
	[InlineData("The Series Title S05E09 1080p WEBRip x264-CAKES")]
	[InlineData("The Show S02E07 1080p WEB H264-EXPLOIT")]
	public void Parse_ShouldReturnNoStreamingProvider_WhenReleaseHasNone(string input)
		=> AssertStreamingProvider(input, null);

	private void AssertStreamingProvider(string input, StreamingProvider? expected)
	{
		var result = _instance.Parse(input);

		Assert.Equal(expected, result);
	}
}
