using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.Parser;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Parser
{
	public class LanguageParserServiceTest
	{
		private readonly IParser<IReadOnlyList<Language>> _instance;

		public LanguageParserServiceTest(ITestOutputHelper output) 
			=> _instance = new LanguageParserService(new XunitLogger<LanguageParserService>(output));

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.English.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.Germany.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.HDTV.XviD-LOL")]
		[InlineData("Title.the.Italian.Series.S01E01.The.Family.720p.HDTV.x264-FTP")]
		[InlineData("Title.the.Italy.Series.S02E01.720p.HDTV.x264-TLA")]
		[InlineData("Series Title - S01E01 - Pilot.en.sub")]
		[InlineData("Series Title - S01E01 - Pilot.eng.sub")]
		[InlineData("Series Title - S01E01 - Pilot.English.sub")]
		[InlineData("Series Title - S01E01 - Pilot.english.sub")]
		[InlineData("Spanish Killroy was Here S02E02 Flodden 720p AMZN WEB-DL DDP5 1 H 264-NTb")]
		[InlineData("Title.the.Spanish.Series.S02E02.1080p.WEB.H264-CAKES")]
		[InlineData("Title.the.Spanish.Series.S02E06.Field.of.Cloth.of.Gold.1080p.AMZN.WEBRip.DDP5.1.x264-NTb")]
		[InlineData("Series Title S09 1080p AMZN WEB-DL H.264 DDP5.1-KiNGS")]
		public void Parse_ShouldReturnEnglish_WhenGivenEnglishRelease(string input) 
			=> AssertLanguage(input, Language.ENGLISH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.French.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.The.1x13.Tueurs.De.Flics.FR.DVDRip.XviD")]
		[InlineData("Series Title Aka Series Alias 2018 S01 1080p FRA Blu-ray AVC LPCM 2.0-HiBOU")]
		public void Parse_ShouldReturnFrench_WhenGivenFrenchRelease(string input) 
			=> AssertLanguage(input, Language.FRENCH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Spanish.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnSpanish_WhenGivenSpanishRelease(string input) 
			=> AssertLanguage(input, Language.SPANISH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.German.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.S04E15.Brotherly.Love.GERMAN.DUBBED.WS.WEBRiP.XviD.REPACK-TVP")]
		[InlineData("The Series Title - S02E16 - Kampfhaehne - mkv - by Videomann")]
		[InlineData("The Title S03 1080i GER Blu-ray AVC DTS-HD MA 2.0-NOGROUP")]
		public void Parse_ShouldReturnGerman_WhenGivenGermanRelease(string input) 
			=> AssertLanguage(input, Language.GERMAN);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Italian.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.1x19.ita.720p.bdmux.x264-novarip")]
		public void Parse_ShouldReturnItalian_WhenGivenItalianRelease(string input) 
			=> AssertLanguage(input, Language.ITALIAN);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Danish.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnDanish_WhenGivenDanishRelease(string input) 
			=> AssertLanguage(input, Language.DANISH);

		[Theory]
		[InlineData("Series Title S02E03 DUTCH 1080p HDTV AAC2.0 HEVC-UGDV")]
		public void Parse_ShouldReturnDutch_WhenGivenDutchRelease(string input) 
			=> AssertLanguage(input, Language.DUTCH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Japanese.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnJapanese_WhenGivenJapaneseRelease(string input) 
			=> AssertLanguage(input, Language.JAPANESE);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Icelandic.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.S01E03.1080p.WEB-DL.DD5.1.H.264-SbR Icelandic")]
		public void Parse_ShouldReturnIcelandic_WhenGivenIcelandicRelease(string input) 
			=> AssertLanguage(input, Language.ICELANDIC);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Chinese.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.Cantonese.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.Mandarin.HDTV.XviD-LOL")]
		[InlineData("[abc] My Series - 01 [CHS]")]
		[InlineData("[abc] My Series - 01 [CHT]")]
		[InlineData("[abc] My Series - 01 [BIG5]")]
		[InlineData("[abc] My Series - 01 [GB]")]
		[InlineData("[abc] My Series - 01 [繁中]")]
		[InlineData("[abc] My Series - 01 [繁体]")]
		[InlineData("[abc] My Series - 01 [简繁外挂]")]
		[InlineData("[abc] My Series - 01 [简繁内封字幕]")]
		[InlineData("[ABC字幕组] My Series - 01 [HDTV]")]
		[InlineData("[喵萌奶茶屋&LoliHouse] 拳愿阿修罗 / Anime Title - 17 [WebRip 1080p HEVC-10bit AAC][中日双语字幕]")]
		public void Parse_ShouldReturnChinese_WhenGivenChineseRelease(string input) 
			=> AssertLanguage(input, Language.CHINESE);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Russian.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.S01E01.1080p.WEB-DL.Rus.TVKlondike")]
		[InlineData("Series.Title.2016.DOCU.RUSSIAN.1080p.WEBRip.DD5.1.x264-FGT")]
		public void Parse_ShouldReturnRussian_WhenGivenRussianRelease(string input) 
			=> AssertLanguage(input, Language.RUSSIAN);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Polish.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.PL.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.PLLEK.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.PL-LEK.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.LEKPL.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.LEK-PL.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.PLDUB.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.PL-DUB.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.DUBPL.HDTV.XviD-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.DUB-PL.HDTV.XviD-LOL")]
		[InlineData("Series.S01E12.Polish.720p.HDTV.x264-PROPLTV")]
		public void Parse_ShouldReturnPolish_WhenGivenPolishRelease(string input) 
			=> AssertLanguage(input, Language.POLISH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Vietnamese.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnVietnamnese_WhenGivenVietnamneseRelease(string input) 
			=> AssertLanguage(input, Language.VIETNAMESE);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Swedish.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnSwedish_WhenGivenSwedishRelease(string input)
			=> AssertLanguage(input, Language.SWEDISH);
		
		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Norwegian.HDTV.XviD-LOL")]
		[InlineData("Bakugan Battle Planet S01E02 NORWEGiAN 1080p WEB H264-NORKiDS")]
		public void Parse_ShouldReturnNorwegian_WhenGivenNorwegianRelease(string input)
			=> AssertLanguage(input, Language.NORWEGIAN);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Finnish.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnFinnish_WhenGivenFinnishRelease(string input)
			=> AssertLanguage(input, Language.FINNISH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Turkish.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnTurkish_WhenGivenTurkishRelease(string input)
			=> AssertLanguage(input, Language.TURKISH);

		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Portuguese.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnPortuguese_WhenGivenPortugueseRelease(string input)
			=> AssertLanguage(input, Language.PORTUGUESE);
		
		[Theory]
		[InlineData("Title.the.Series.S01E01.FLEMISH.HDTV.x264-BRiGAND")]
		[InlineData("Title.the.Series.S01E05.FLEMISH.720p.WEB.H264-MERCATOR")]
		public void Parse_ShouldReturnFlemish_WhenGivenFlemishRelease(string input)
			=> AssertLanguage(input, Language.FLEMISH);
		
		[Theory]
		[InlineData("Title.the.Series.S03E13.Greek.PDTV.XviD-Ouzo")]
		[InlineData("Formula 1 Drive To Survive S03 Greek 1080p NF WEB-DL x265 DDP Atmos 5.1-deef")]
		public void Parse_ShouldReturnGreek_WhenGivenGreekRelease(string input)
			=> AssertLanguage(input, Language.GREEK);
		
		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.Korean.HDTV.XviD-LOL")]
		public void Parse_ShouldReturnKorean_WhenGivenKoreanRelease(string input) 
			=> AssertLanguage(input, Language.KOREAN);
		
		[Theory]
		[InlineData("Title.the.Series.2009.S01E14.HDTV.XviD.HUNDUB-LOL")]
		[InlineData("Title.the.Series.2009.S01E14.HDTV.XviD.HUN-LOL")]
		public void Parse_ShouldReturnHungarian_WhenGivenHungarianRelease(string input) 
			=> AssertLanguage(input, Language.HUNGARIAN);
		
		[Theory]
		[InlineData("Title.the.Series.S01-03.DVDRip.HebDub")]
		public void Parse_ShouldReturnHebrew_WhenGivenHebrewRelease(string input) 
			=> AssertLanguage(input, Language.HEBREW);
		
		[Theory]
		[InlineData("Title.the.Series.S05E01.WEBRip.x264.AC3.LT.EN-CNN")]
		[InlineData("Gang.Wars.Pythons.S01E05.LiTHUANiAN.1080p.WEB.h264-XME")]
		public void Parse_ShouldReturnLithuanian_WhenGivenLithuanianRelease(string input) 
			=> AssertLanguage(input, Language.LITHUANIAN);
		
		[Theory]
		[InlineData("Title.the.Series.S07E11.WEB Rip.XviD.Louige-CZ.5.1")]
		public void Parse_ShouldReturnCzech_WhenGivenCzechRelease(string input) 
			=> AssertLanguage(input, Language.CZECH);
		
		[Theory]
		[InlineData("Series Title.S01.ARABIC.COMPLETE.720p.NF.WEBRip.x264-PTV")]
		public void Parse_ShouldReturnArabic_WhenGivenArabicRelease(string input) 
			=> AssertLanguage(input, Language.ARABIC);
		
		[Theory]
		[InlineData("The Shadow Series S01 E01-08 WebRip Dual Audio [Hindi 5.1 + English 5.1] 720p x264 AAC ESub")]
		[InlineData("Anime Title (2020) S04 Complete 720p NF WEBRip [Hindi+English] Dual audio")]
		public void Parse_ShouldReturnHindi_WhenGivenHindiRelease(string input) 
			=> AssertLanguage(input, Language.HINDI);
		
		[Theory]
		[InlineData("Hausen.S01E01-02.ITA.GER.1080p.WEB.DD5.1.H264-MeM", new [] { Language.ITALIAN, Language.GERMAN })]
		public void Parse_ShouldReturnExpectedLanguages_WhenGivenExplicitMultiRelease(string input, IReadOnlyCollection<Language> expected)
		{
			var parsed = _instance.Parse(input);
			
			Assert.True(parsed.Count == expected.Count);
			Assert.All(parsed, language => Assert.Contains(language, expected));
		}

		[Theory]
		[InlineData("Title.the.Russian.Series.S01E07.Cold.Action.HDTV.XviD-Droned")]
		[InlineData("Title.the.Russian.Series.S01E07E08.Cold.Action.HDTV.XviD-Droned")]
		[InlineData("Title.the.Russian.Series.S01.1080p.WEBRip.DDP5.1.x264-Drone")]
		[InlineData("Title.the.Spanish.Series.S02E08.Peace.1080p.AMZN.WEBRip.DDP5.1.x264-NTb")]
		[InlineData("Title The Spanish S02E02 Flodden 720p AMZN WEB-DL DDP5 1 H 264-NTb")]
		public void Parse_ShouldNotParseSeriesOrEpisodeTitle(string input)
			=> AssertLanguage(input, Language.ENGLISH);

		private void AssertLanguage(string input, Language expected)
		{
			var parsed = _instance.Parse(input);
			
			Assert.True(parsed.Count == 1);
			Assert.Equal(expected, parsed[0]);
		}
	}
}
