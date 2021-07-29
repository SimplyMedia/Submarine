using Submarine.Core.Parser;
using Submarine.Core.Quality;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Quality
{
	public class QualityParserServiceTest
	{
		private readonly XunitLogger<QualityParserService> _logger;
		private readonly QualityParserService _instance;

		public QualityParserServiceTest(ITestOutputHelper output){
			_logger = new XunitLogger<QualityParserService>(output);
			_instance = new QualityParserService(_logger);
		}

		[Theory]
		[InlineData("Movie Name Part II 2020 1080p WEB-DL DD 5.1 H264-LOST", QualitySource.WEB_DL)]
		[InlineData("Series Title !- 2 S2 [Blu-ray][M2TS (A)][16:9][h264][1080p][PCM 2.0][Dual Audio][Softsubs]", QualitySource.BLURAY)]
		[InlineData("[-ZR-] Series Title 2 S2 [Blu-ray][MKV][h264][1080p][AAC 2.0][Dual Audio][Softsubs (-ZR-)]", QualitySource.BLURAY)]
		[InlineData("Movie 2013 720p BluRay FLAC5.1 H.264", QualitySource.BLURAY)]
		[InlineData("Movie AKA Movie Alias 1986 1080p BluRay DTS 2.0 x264-HeavyWeight", QualitySource.BLURAY)]
		[InlineData("Movie Name 1978 1080p BluRay REMUX AVC FLAC 1.0-BLURANiUM", QualitySource.BLURAY_REMUX)]
		[InlineData("Movie AKA Alias 2017 S03 1080p WEB-DL AAC 2.0 H.264-AppleTor", QualitySource.WEB_DL)]
		[InlineData("Series.Name.S04E02.Top.50.Mystery.Box.1080p.AMZN.WEB-DL.DDP2.0.H.264-FLUX", QualitySource.WEB_DL)]
		[InlineData("Some.Good.Series.S09E10.Episode.10.1080p.AMZN.WEB-DL.DDP5.1.H.264-NTb", QualitySource.WEB_DL)]
		[InlineData("Movie 3D 2013 BluRay 1080p x264 DTS-CMCT", QualitySource.BLURAY)]
		[InlineData("Series S03 1080p WEB-DL DD5 1 H264", QualitySource.WEB_DL)]
		[InlineData("[SubsPlease] Anime Title - 04 (1080p) [A27AA2EF]", QualitySource.TV)]
		[InlineData("Do.Not.Get.Pregnant.2.S10E20.REPACK.720p.WEB.h264-BAE", QualitySource.WEB_DL)]
		[InlineData("Series.Title.S01E01.RawHD-2hd", QualitySource.RAW_HD)]
		[InlineData("Series Title in Some other Language - 40 BD XviD MP3 4329FA2F", QualitySource.BLURAY)]
		public void Parse_QualitySource_AsExpected(string input, QualitySource source)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.Equal(source, parsed.QualityResolutionModel.QualitySource);
		}

		[Theory]
		[InlineData("IT.DO.BE.QUIET.Part.II.2020.THAI.2160p.UHD.BLURAY.X265-HOA", QualityResolution.R2160_P)]
		[InlineData("Series S17e15 1080p Hevc X265 Megusta", QualityResolution.R1080_P)]
		[InlineData("Series 2021 1080p S01E49 WEB-DL AAC H.264", QualityResolution.R1080_P)]
		[InlineData("Some.Nice.Movie.2021.Ger.Spa.WEBRip.x264-YG⭐", QualityResolution.R720_P)]
		[InlineData("Anime Title - 15 (2021) [Golumpa] [English Dubbed] [WEBRip] [HD 720p]", QualityResolution.R720_P)]
		[InlineData("Do.Not.Get.Pregnant.2.S10E20.REPACK.720p.WEB.h264-BAE", QualityResolution.R720_P)]
		[InlineData("A Safe Place 1971 360p WEB-DL AAC x264", QualityResolution.R360_P)]
		[InlineData("Really.long.movie.title.yea.2021.07.15.540p.WEBDL-Anon", QualityResolution.R540_P)]
		[InlineData("Movie-Title 1979 576p BDRip DD2.0 x264 NoGroup", QualityResolution.R576_P)]
		[InlineData("[SubsPlease] Anime Title - 15 (480p) [BDE17E52].mkv", QualityResolution.R480_P)]
		[InlineData("Some.Movie.Title.2007.MULTi.COMPLETE.UHD.BLURAY-DUPLiKAT", QualityResolution.R2160_P)]
		[InlineData("Series.Title.S23E17.HDTV.x264-PHOENiX[TGx]", QualityResolution.R720_P)]
		[InlineData("A very long Movie Title 2012 SDTV MP3 2.0-NoGroup", QualityResolution.R480_P)]
		[InlineData("Series Title in Some other Language - 40 BD XviD MP3 4329FA2F", QualityResolution.R480_P)]
		public void Parse_QualityResolution_AsExpected(string input, QualityResolution resolution)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.Equal(resolution, parsed.QualityResolutionModel.Resolution);
		}

		
		[Theory]
		[InlineData("Movie 1963 REPACK 1080p BluRay REMUX AVC FLAC 1 0-BLURANiUM")]
		[InlineData("11 Never Dying Elders 2019 REPACK 1080p BluRay REMUX AVC DTS-HD MA 5.1-NOAH")]
		[InlineData("A.Long.Movie.Title.2019.NORDiC.REPACK.720p.BluRay.x264.DTS5.1-TWA")]
		[InlineData("Out.Of.There.League.UK.S13E04.REPACK.720p.HDTV.x264-FaiLED")]
		[InlineData("Do.Not.Get.Pregnant.2.S10E20.REPACK.720p.WEB.h264-BAE")]
		public void Parse_RevisionRepack_ReturnTrue(string input)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.True(parsed.Revision.IsRepack);
		}
		
		[Theory]
		[InlineData("Repacking for school.720p.WEB")]
		public void Parse_RevisionRepack_ReturnFalse(string input)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.False(parsed.Revision.IsRepack);
		}

		[Theory]
		[InlineData("Everybody 2021 PROPER 1080p UHD BluRay DD+ 7.1 x264-LoRD")]
		[InlineData("Series S01 PROPER COMPLETE 1080p DSNP WEB-DL DD+ 5.1 H264-TOMMY")]
		[InlineData("Best.Anime.Movie.For.You.2019.PROPER.1080p.BluRay.x264-HAiKU")]
		[InlineData("The Golden Episode 2008 PROPER DVDRip XviD-WRD")]
		[InlineData("Dr.Death.S01E04.PROPER.1080p.WEB.H264-KOGi")]
		public void Parse_RevisionProper_ReturnIncreasedVersion(string input)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.Equal(2, parsed.Revision.Version);
		}

		[Theory]
		[InlineData("Series 2018 S03E15 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
		[InlineData("The Series S04E02 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
		[InlineData("Series 2017 S04E10 1080p WEB-DL AAC 2.0 H264-PLZPROPER")]
		[InlineData("Series S01E17 720p WEB-DL AAC 2.0 H264-PLZPROPER")]
		public void Parse_RevisionProper_ReturnDefaultVersion(string input)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.Equal(1, parsed.Revision.Version);
		}

		[Theory]
		[InlineData("[Kulot] Anime Title v3 [Dual-Audio][BDRip 1836x996 x264 FLACx2] | Complete | Kidou Senshi Gundam F91", 3)]
		[InlineData("[SubsPlease] Anime Title - 01v2 (1080p) [CD04C72E].mkv", 2)]
		[InlineData("[Erai-raws] Anime 3rd Season - 14 [v0][1080p][Multiple Subtitle].mkv", 0)]
		[InlineData("Sister What 1963 S10 1080p BluRay DTS 2.0 x264-OUIJA", 1)]
		public void Parse_RevisionVersion_ReturnVersion(string input, int version)
		{
			var parsed = _instance.ParseQuality(input);
			
			Assert.Equal(version, parsed.Revision.Version);
		}
	}
}