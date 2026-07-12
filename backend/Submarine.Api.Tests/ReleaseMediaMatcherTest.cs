using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Core.Release;
using Xunit;

namespace Submarine.Api.Tests;

public class ReleaseMediaMatcherTest
{
	[Theory]
	[InlineData("The Show!", "the show")]
	[InlineData("SHOW", "show")]
	[InlineData("Show:  The   Return", "show the return")]
	[InlineData("Marvel's Show", "marvel s show")]
	public void Normalize_ShouldLowercaseStripAndCollapse_WhenGivenPunctuation(string input, string expected)
		=> Assert.Equal(expected, ReleaseMediaMatcher.Normalize(input));

	[Fact]
	public void MatchSeries_ShouldMatchIgnoringPunctuationAndCase_WhenTitleMatches()
	{
		var matcher = new ReleaseMediaMatcher(new[] { Series("The Office", tvdbId: 7) }, Array.Empty<Movie>());

		var match = matcher.MatchSeries(Release("the office!"), tvdbId: null);

		Assert.NotNull(match);
		Assert.Equal(7, match!.TvdbId);
	}

	[Fact]
	public void MatchSeries_ShouldMatchByTvdbId_WhenTitleDiffers()
	{
		var matcher = new ReleaseMediaMatcher(new[] { Series("Original Title", tvdbId: 42) }, Array.Empty<Movie>());

		var match = matcher.MatchSeries(Release("completely different"), tvdbId: 42);

		Assert.NotNull(match);
		Assert.Equal(42, match!.TvdbId);
	}

	[Theory]
	[InlineData(2021, true)]
	[InlineData(2022, true)]
	[InlineData(2015, false)]
	public void MatchMovie_ShouldHonorYearTolerance_WhenTitleMatches(int releaseYear, bool expectMatch)
	{
		var matcher = new ReleaseMediaMatcher(Array.Empty<Series>(),
			new[] { new Movie { Id = 1, Title = "Dune", Year = 2021 } });

		var release = Release("dune") with { Year = releaseYear };

		Assert.Equal(expectMatch, matcher.MatchMovie(release) != null);
	}

	private static Series Series(string title, int tvdbId)
		=> new() { Id = tvdbId, Title = title, TvdbId = tvdbId };

	private static BaseRelease Release(string title)
		=> new() { Title = title, Aliases = Array.Empty<string>() };
}
