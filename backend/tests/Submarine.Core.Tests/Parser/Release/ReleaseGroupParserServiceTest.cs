using Submarine.Core.Parser;
using Submarine.Core.Parser.Release;
using Xunit;

namespace Submarine.Core.Tests.Parser.Release;

public class ReleaseGroupParserServiceTest
{
	private readonly IParser<string?> _instance;

	public ReleaseGroupParserServiceTest(ITestOutputHelper output)
		=> _instance = new ReleaseGroupParserService(new XunitLogger<ReleaseGroupParserService>(output));

	[Theory]
	// Standard trailing release group
	[InlineData("Movie.Title.2020.1080p.BluRay.x264-GROUP", "GROUP")]
	// Anime subgroup
	[InlineData("[SubsPlease] Anime Show - 01 (1080p) [ABCD1234].mkv", "SubsPlease")]
	// Exact exception groups that don't follow -RlsGrp
	[InlineData("Some.Movie.2019.1080p.BluRay.x264-YTS.MX", "YTS.MX")]
	[InlineData("Some.Movie.2020.1080p.BluRay.x264-D-Z0N3", "D-Z0N3")]
	// Exception groups that end with RlsGroup] or RlsGroup)
	[InlineData("Movie Title 2020 1080p BluRay x264 [Tigole]", "Tigole")]
	// Trailing date must not be treated as the release group (merged negative lookbehind)
	[InlineData("Show.Name.1080p.WEB-DL-GROUP 2020-01", "GROUP")]
	public void Parse_ShouldParseReleaseGroup(string input, string expected)
	{
		var parsed = _instance.Parse(input);

		Assert.Equal(expected, parsed);
	}

	[Theory]
	[InlineData("Movie.Title.2020.1080p.WEB-DL")]
	[InlineData("Movie.Title.2020.1080p.BluRay")]
	public void Parse_ShouldReturnNull_WhenNoReleaseGroupPresent(string input)
	{
		var parsed = _instance.Parse(input);

		Assert.Null(parsed);
	}
}
