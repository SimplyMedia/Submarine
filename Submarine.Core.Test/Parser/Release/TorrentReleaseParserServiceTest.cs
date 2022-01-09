using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Submarine.Core.Release.Torrent;
using Xunit;
using Xunit.Abstractions;

namespace Submarine.Core.Test.Parser.Release;

public class TorrentReleaseParserServiceTest
{
	private readonly IParser<TorrentRelease> _instance;

	public TorrentReleaseParserServiceTest(ITestOutputHelper output)
		=> _instance = new TorrentReleaseParserService(new XunitLogger<TorrentReleaseParserService>(output),
			new ReleaseParserService(new XunitLogger<ReleaseParserService>(output),
				new LanguageParserService(new XunitLogger<LanguageParserService>(output)),
				new StreamingProviderParserService(new XunitLogger<StreamingProviderParserService>(output)),
				new QualityParserService(new XunitLogger<QualityParserService>(output)),
				new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output))));

	[Theory]
	[InlineData("Anime Show AKA Japanese Name S01 1080p WEB-DL Dual-Audio AAC 2.0 H.264-ZR", "Japanese Name",
		new[] { "Anime Show" })]
	[InlineData("Wheel of Poverty and Reality AKA Other Language Title 2021 1080p BluRay REMUX AVC DTS-HD MA 5.1-C0M3T",
		"Other Language Title", new[] { "Wheel of Poverty and Reality" })]
	[InlineData("Title One AKA Title Two AKA Title Three AKA Title Four S04 1080p WEB-DL AAC 2.0 H.264-NOGROUP",
		"Title Four", new[] { "Title One", "Title Two", "Title Three" })]
	public void Parse_ShouldParseAliases_WhenTitleContainsAKA(string input, string title, string[] aliases)
	{
		var res = _instance.Parse(input);

		Assert.Equal(input, res.FullTitle);
		Assert.Equal(title, res.Title);
		Assert.Equal(aliases, res.Aliases);
	}
}
