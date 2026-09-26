using Shouldly;
using Submarine.Core.Search;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class TitleMatcherTests
{
	[Fact]
	public void IsMatch_ShouldMatch_WhenTitleIsIdenticalIgnoringPunctuationAndCase()
	{
		TitleMatcher.IsMatch("The Show!", null, ["the show"], null).ShouldBeTrue();
	}

	[Fact]
	public void IsMatch_ShouldMatch_WhenAnyAliasMatches()
	{
		TitleMatcher.IsMatch("Alt Name", null, ["Main Title", "Alt Name"], null).ShouldBeTrue();
	}

	[Fact]
	public void IsMatch_ShouldReject_WhenNoCandidateTitleMatches()
	{
		TitleMatcher.IsMatch("Unrelated Show", null, ["The Show"], null).ShouldBeFalse();
	}

	[Fact]
	public void IsMatch_ShouldAcceptOneYearOfTolerance()
	{
		TitleMatcher.IsMatch("The Show", 2021, ["The Show"], 2020).ShouldBeTrue();
	}

	[Fact]
	public void IsMatch_ShouldReject_WhenYearsDisagreeByMoreThanOneYear()
	{
		TitleMatcher.IsMatch("The Show", 2019, ["The Show"], 2023).ShouldBeFalse();
	}

	[Fact]
	public void IsMatch_ShouldAccept_WhenEitherYearIsUnknown()
	{
		TitleMatcher.IsMatch("The Show", null, ["The Show"], 2020).ShouldBeTrue();
		TitleMatcher.IsMatch("The Show", 2020, ["The Show"], null).ShouldBeTrue();
	}
}
