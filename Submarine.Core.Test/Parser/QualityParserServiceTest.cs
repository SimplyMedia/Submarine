using Submarine.Core.Parser;
using Submarine.Core.Quality;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Parser;

public class QualityParserServiceTest
{
	private readonly IParser<QualityModel> _instance;

	public QualityParserServiceTest(ITestOutputHelper output)
		=> _instance = new QualityParserService(new XunitLogger<QualityParserService>(output));

	[Theory]
	[InlineData("Movie Name 1978 1080p BluRay REMUX AVC FLAC 1.0-BLURANiUM")]
	[InlineData("Series!!! on ICE - S01E12[JP BD Remux][ENG subs]")]
	[InlineData("Series.Title.S01E08.The.Well.BluRay.1080p.AVC.DTS-HD.MA.5.1.REMUX-FraMeSToR")]
	[InlineData("Series.Title.2x11.Nato.Per.La.Truffa.Bluray.Remux.AVC.1080p.AC3.ITA")]
	[InlineData("Series.Title.2x11.Nato.Per.La.Truffa.Bluray.Remux.AVC.AC3.ITA")]
	[InlineData("Series.Title.S03E01.The.Calm.1080p.DTS-HD.MA.5.1.AVC.REMUX-FraMeSToR")]
	public void Parse_ShouldReturnQualitySourceBlurayRemux_WhenReleaseIsBlurayRemux(string input)
		=> AssertQualitySource(input, QualitySource.BLURAY_REMUX);

	[Theory]
	[InlineData("[-ZR-] Series Title 2 S2 [Blu-ray][MKV][h264][1080p][AAC 2.0][Dual Audio][Softsubs (-ZR-)]")]
	[InlineData("Movie 2013 720p BluRay FLAC5.1 H.264")]
	[InlineData("Movie AKA Movie Alias 1986 1080p BluRay DTS 2.0 x264-HeavyWeight")]
	[InlineData("Movie 3D 2013 BluRay 1080p x264 DTS-CMCT")]
	[InlineData("Series Title in Some other Language - 40 BD XviD MP3 4329FA2F")]
	public void Parse_ShouldReturnQualitySourceBluray_WhenReleaseIsBluray(string input)
		=> AssertQualitySource(input, QualitySource.BLURAY);

	[Theory]
	[InlineData("Movie Name Part II 2020 1080p WEB-DL DD 5.1 H264-LOST")]
	[InlineData("Movie AKA Alias 2017 S03 1080p WEB-DL AAC 2.0 H.264-AppleTor")]
	[InlineData("Series.Name.S04E02.Top.50.Mystery.Box.1080p.AMZN.WEB-DL.DDP2.0.H.264-FLUX")]
	[InlineData("Some.Good.Series.S09E10.Episode.10.1080p.AMZN.WEB-DL.DDP5.1.H.264-NTb")]
	[InlineData("Series S03 1080p WEB-DL DD5 1 H264")]
	[InlineData("The.Series.S01E10.The.Leviathan.480p.WEB-DL.x264-mSD")]
	[InlineData("The.Series.S04E10.Glee.Actually.480p.WEB-DL.x264-mSD")]
	[InlineData("The.SeriesS06E11.The.Santa.Simulation.480p.WEB-DL.x264-mSD")]
	[InlineData("The.Series.S02E04.480p.WEB.DL.nSD.x264-NhaNc3")]
	[InlineData("The.Series.S01E08.Das.geloeschte.Ich.German.Dubbed.DL.AmazonHD.x264-TVS")]
	[InlineData("The.Series.S01E04.Rod.Trip.mit.meinem.Onkel.German.DL.NetflixUHD.x264")]
	[InlineData("[HorribleSubs] Series Title! S01 [Web][MKV][h264][480p][AAC 2.0][Softsubs (HorribleSubs)]")]
	public void Parse_ShouldReturnQualitySourceWebDL_WhenReleaseIsWebDL(string input)
		=> AssertQualitySource(input, QualitySource.WEB_DL);

	[Theory]
	[InlineData("The.Series.S02E10.480p.HULU.WEBRip.x264-Puffin")]
	[InlineData("The.Series.S10E14.Techs.And.Balances.480p.AE.WEBRip.AAC2.0.x264-SEA")]
	[InlineData("Series.Title.1x04.ITA.WEBMux.x264-NovaRip")]
	public void Parse_ShouldReturnQualitySourceWebRip_WhenReleaseIsWebRip(string input)
		=> AssertQualitySource(input, QualitySource.WEB_RIP);

	[Theory]
	[InlineData("The.Series.S01E13.NTSC.x264-CtrlSD")]
	[InlineData("The.Series.S03E06.DVDRip.XviD-WiDE")]
	[InlineData("The.Series.S03E06.DVD.Rip.XviD-WiDE")]
	[InlineData("the.Series.1x13.circles.ws.xvidvd-tns")]
	[InlineData("the_Series.9x18.sunshine_days.ac3.ws_dvdrip_xvid-fov.avi")]
	[InlineData("[FroZen] Series - 23 [DVD][7F6170E6]")]
	[InlineData("[AniDL] Series - 26 -[360p][DVD][D - A][Exiled - Destiny]")]
	public void Parse_ShouldReturnQualitySourceDVD_WhenReleaseIsDVD(string input)
		=> AssertQualitySource(input, QualitySource.DVD);

	[Theory]
	[InlineData("Series.Title.S01E01.RawHD-2hd")]
	[InlineData("POI S02E11 1080i HDTV DD5.1 MPEG2-TrollHD")]
	[InlineData("How I Met Your Developer S01E18 Nothing Good Happens After Submarine 720p HDTV DD5.1 MPEG2-TrollHD")]
	[InlineData("The Series S01E11 The Finals 1080i HDTV DD5.1 MPEG2-TrollHD")]
	[InlineData("Series.Title.S07E11.1080i.HDTV.DD5.1.MPEG2-NTb.ts")]
	[InlineData("Game of Series S04E10 1080i HDTV MPEG2 DD5.1-CtrlHD.ts")]
	[InlineData("Series.Title.S02E05.1080i.HDTV.DD2.0.MPEG2-NTb.ts")]
	[InlineData("Show - S03E01 - Episode Title Raw-HD.ts")]
	[InlineData("Series.Title.S10E09.Title.1080i.UPSCALE.HDTV.DD5.1.MPEG2-zebra")]
	[InlineData("Series.Title.2011-08-04.1080i.HDTV.MPEG-2-CtrlHD")]
	public void Parse_ShouldReturnQualitySourceRawHD_WhenReleaseIsRawHD(string input)
		=> AssertQualitySource(input, QualitySource.RAW_HD);

	[Theory]
	[InlineData("The Series S02E01 HDTV XviD 2HD")]
	[InlineData("The Series S05E11 PROPER HDTV XviD 2HD")]
	[InlineData("The Series Show S02E08 HDTV x264 FTP")]
	[InlineData("The.Series.2011.S02E01.WS.PDTV.x264-TLA")]
	[InlineData("The.Series.2011.S02E01.WS.PDTV.x264-REPACK-TLA")]
	[InlineData("The Series S11E03 has no periods or extension HDTV")]
	[InlineData("The.Series.S04E05.HDTV.XviD-LOL")]
	[InlineData("The.Series.S03E06.HDTV-WiDE")]
	[InlineData("Series.S10E27.WS.DSR.XviD-2HD")]
	[InlineData("The.Series.S03.TVRip.XviD-NOGRP")]
	public void Parse_ShouldReturnQualitySourceTV_WhenReleaseIsTV(string input)
		=> AssertQualitySource(input, QualitySource.TV);

	[Theory]
	[InlineData("[SubsPlease] Anime Title - 04 (1080p) [A27AA2EF]")]
	[InlineData("[HorribleSubs] The Series - 32 [480p]")]
	[InlineData("[CR] The Series - 004 [480p][48CE2D0F]")]
	[InlineData("[Hatsuyuki] The Series - 363 [848x480][ADE35E38]")]
	[InlineData("The Series S01E04 DSR x264 2HD")]
	[InlineData("The Series S01E04 Series Death Train DSR x264 MiNDTHEGAP")]
	public void Parse_ShouldReturnQualitySourceTV_WhenNoQualitySourceFound(string input)
		=> AssertQualitySource(input, QualitySource.TV);

	[Theory]
	[InlineData("IT.DO.BE.QUIET.Part.II.2020.THAI.2160p.UHD.BLURAY.X265-HOA")]
	[InlineData("Some.Movie.Title.2007.MULTi.COMPLETE.UHD.BLURAY-DUPLiKAT")]
	public void Parse_ShouldReturnQualityResolution2160p_WhenReleaseIs2160p(string input)
		=> AssertQualityResolution(input, QualityResolution.R2160_P);

	[Theory]
	[InlineData("Series S17e15 1080p Hevc X265 Megusta")]
	[InlineData("Series 2021 1080p S01E49 WEB-DL AAC H.264")]
	public void Parse_ShouldReturnQualityResolution1080p_WhenReleaseIs1080p(string input)
		=> AssertQualityResolution(input, QualityResolution.R1080_P);

	[Theory]
	[InlineData("Some.Nice.Movie.2021.Ger.Spa.WEBRip.x264-YG⭐")]
	[InlineData("Anime Title - 15 (2021) [Golumpa] [English Dubbed] [WEBRip] [HD 720p]")]
	[InlineData("Do.Not.Get.Pregnant.2.S10E20.REPACK.720p.WEB.h264-BAE")]
	[InlineData("Series.Title.S23E17.HDTV.x264-PHOENiX[TGx]")]
	public void Parse_ShouldReturnQualityResolution720p_WhenReleaseIs720p(string input)
		=> AssertQualityResolution(input, QualityResolution.R720_P);

	[Theory]
	[InlineData("Movie-Title 1979 576p BDRip DD2.0 x264 NoGroup")]
	public void Parse_ShouldReturnQualityResolution576p_WhenReleaseIs576p(string input)
		=> AssertQualityResolution(input, QualityResolution.R576_P);

	[Theory]
	[InlineData("Really.long.movie.title.yea.2021.07.15.540p.WEBDL-Anon")]
	public void Parse_ShouldReturnQualityResolution540p_WhenReleaseIs540p(string input)
		=> AssertQualityResolution(input, QualityResolution.R540_P);

	[Theory]
	[InlineData("[SubsPlease] Anime Title - 15 (480p) [BDE17E52].mkv")]
	[InlineData("A very long Movie Title 2012 SDTV MP3 2.0-NoGroup")]
	[InlineData("Series Title in Some other Language - 40 BD XviD MP3 4329FA2F")]
	public void Parse_ShouldReturnQualityResolution480p_WhenReleaseIs480p(string input)
		=> AssertQualityResolution(input, QualityResolution.R480_P);

	[Theory]
	[InlineData("A Safe Place 1971 360p WEB-DL AAC x264")]
	public void Parse_ShouldReturnQualityResolution360p_WhenReleaseIs360p(string input)
		=> AssertQualityResolution(input, QualityResolution.R360_P);


	[Theory]
	[InlineData("Movie 1963 REPACK 1080p BluRay REMUX AVC FLAC 1 0-BLURANiUM")]
	[InlineData("11 Never Dying Elders 2019 REPACK 1080p BluRay REMUX AVC DTS-HD MA 5.1-NOAH")]
	[InlineData("A.Long.Movie.Title.2019.NORDiC.REPACK.720p.BluRay.x264.DTS5.1-TWA")]
	[InlineData("Out.Of.There.League.UK.S13E04.REPACK.720p.HDTV.x264-FaiLED")]
	[InlineData("Do.Not.Get.Pregnant.2.S10E20.REPACK.720p.WEB.h264-BAE")]
	public void Parse_ShouldSetIsRepackTrue_WhenReleaseIsRepack(string input)
		=> AssertRevisionIsRepack(input, true);

	[Theory]
	[InlineData("Repacking for school.720p.WEB")]
	public void Parse_ShouldSetIsRepackFalse_WhenRepackIsInTitle(string input)
		=> AssertRevisionIsRepack(input, false);

	[Theory]
	[InlineData("Everybody 2021 PROPER 1080p UHD BluRay DD+ 7.1 x264-LoRD")]
	[InlineData("Series S01 PROPER COMPLETE 1080p DSNP WEB-DL DD+ 5.1 H264-TOMMY")]
	[InlineData("Best.Anime.Movie.For.You.2019.PROPER.1080p.BluRay.x264-HAiKU")]
	[InlineData("The Golden Episode 2008 PROPER DVDRip XviD-WRD")]
	[InlineData("The.Show.S01E04.PROPER.1080p.WEB.H264-KOGi")]
	public void Parse_ShouldIncreaseRevisionVersion_WhenReleaseIsProper(string input)
		=> AssertRevisionVersion(input, 2);

	[Theory]
	[InlineData("Series 2018 S03E15 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
	[InlineData("The Series S04E02 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
	[InlineData("Series 2017 S04E10 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
	[InlineData("Series S01E17 720p WEB-DL AAC 2.0 H264-PLZPROPER")]
	public void Parse_ShouldNotIncreaseRevisionVersion_WhenGroupNameIncludesProper(string input)
		=> AssertRevisionVersion(input, 1);

	[Theory]
	[InlineData("[Kulot] Anime Title v3 [Dual-Audio][BDRip 1836x996 x264 FLACx2] | Complete | The Anime Title F91", 3)]
	[InlineData("[SubsPlease] Anime Title - 01v2 (1080p) [CD04C72E].mkv", 2)]
	[InlineData("[Erai-raws] Anime 3rd Season - 14 [v0][1080p][Multiple Subtitle].mkv", 0)]
	public void Parse_ShouldIncreaseRevisionVersion_WhenVersionExistsInRelease(string input, int expectedVersion)
		=> AssertRevisionVersion(input, expectedVersion);

	[Theory]
	[InlineData("Sister What 1963 S10 1080p BluRay DTS 2.0 x264-OUIJA")]
	public void Parse_ShouldNotIncreaseRevisionVersion_WhenNoVersionExistsInRelease(string input)
		=> AssertRevisionVersion(input, 1);

	private void AssertRevisionIsRepack(string input, bool expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Revision.IsRepack);
	}

	private void AssertRevisionVersion(string input, int expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Revision.Version);
	}

	private void AssertQualitySource(string input, QualitySource expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Resolution.QualitySource);
	}

	private void AssertQualityResolution(string input, QualityResolution expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed.Resolution.Resolution);
	}
}
