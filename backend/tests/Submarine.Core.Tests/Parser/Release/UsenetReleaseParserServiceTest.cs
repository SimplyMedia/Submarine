using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Release.Usenet;
using Submarine.Core.Validator;
using Xunit;

namespace Submarine.Core.Tests.Parser.Release;

public class UsenetReleaseParserServiceTest
{
	private readonly IParser<UsenetRelease> _instance;

	public UsenetReleaseParserServiceTest(ITestOutputHelper output)
	{
		var releaseParserService = new ReleaseParserService(new XunitLogger<ReleaseParserService>(output),
			new LanguageParserService(new XunitLogger<LanguageParserService>(output)),
			new StreamingProviderParserService(new XunitLogger<StreamingProviderParserService>(output)),
			new QualityParserService(new XunitLogger<QualityParserService>(output)),
			new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output)),
			new StaticQualityOverrideSource());

		_instance = new UsenetReleaseParserService(new XunitLogger<UsenetReleaseParserService>(output),
			new UsenetReleaseValidatorService(new XunitLogger<UsenetReleaseValidatorService>(output)),
			releaseParserService);
	}

	[Theory]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-NZBGeek")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-Obfuscated")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-Obfuscation")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-Scrambled")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-postbot")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-xpost")]
	[InlineData("Show.Name.S01E01.1080p.WEB.H264-GROUP-RePACKPOST")]
	public void Parse_ShouldStripUsenetJunkSuffix_AndKeepTitle(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal("Show Name", parsed.Title);
	}

	[Theory]
	// Reversed title of "The.Office.S01E10.720p.HDTV.x264-GROUP"
	[InlineData("PUORG-462x.VTDH.p027.01E10S.eciffO.ehT", "The Office")]
	public void Parse_ShouldReverseTitle_WhenTitleIsReversed(string input, string title)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(title, parsed.Title);
	}
}
