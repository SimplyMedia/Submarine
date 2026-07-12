using System;
using Submarine.Core.Indexer.Torznab;
using Xunit;

namespace Submarine.Core.Tests.Indexer.Torznab;

public class TorznabRequestBuilderTest
{
	private readonly TorznabRequestBuilder _instance = new("secret");

	[Fact]
	public void BuildCapsQuery_ShouldIncludeApiKey_WhenApiKeyProvided()
		=> Assert.Equal("?t=caps&apikey=secret", _instance.BuildCapsQuery());

	[Fact]
	public void BuildCapsQuery_ShouldOmitApiKey_WhenApiKeyIsNull()
		=> Assert.Equal("?t=caps", new TorznabRequestBuilder(null).BuildCapsQuery());

	[Fact]
	public void BuildSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> Assert.Equal("?t=search&apikey=secret&q=ubuntu&cat=5030,5040&limit=100&offset=50",
			_instance.BuildSearchQuery("ubuntu", new[] { 5030, 5040 }, 100, 50));

	[Fact]
	public void BuildSearchQuery_ShouldOmitNullParams_WhenOnlyQueryProvided()
		=> Assert.Equal("?t=search&apikey=secret&q=ubuntu", _instance.BuildSearchQuery("ubuntu"));

	[Fact]
	public void BuildSearchQuery_ShouldOmitCat_WhenCategoriesEmpty()
		=> Assert.Equal("?t=search&apikey=secret", _instance.BuildSearchQuery(categories: Array.Empty<int>()));

	[Theory]
	[InlineData("Ubuntu 22.04 & More", "Ubuntu%2022.04%20%26%20More")]
	[InlineData("C#/.NET?", "C%23%2F.NET%3F")]
	public void BuildSearchQuery_ShouldEscapeQuery_WhenQueryContainsReservedChars(string query, string expected)
		=> Assert.Equal($"?t=search&apikey=secret&q={expected}", _instance.BuildSearchQuery(query));

	[Fact]
	public void BuildTvSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> Assert.Equal(
			"?t=tvsearch&apikey=secret&q=The%20Expanse&season=2&ep=5&tvdbid=280619&imdbid=tt3230854&rid=41393",
			_instance.BuildTvSearchQuery("The Expanse", 2, 5, 280619, "tt3230854", 41393));

	[Fact]
	public void BuildTvSearchQuery_ShouldOmitNullParams_WhenOnlyTvdbIdProvided()
		=> Assert.Equal("?t=tvsearch&apikey=secret&tvdbid=280619", _instance.BuildTvSearchQuery(tvdbId: 280619));

	[Fact]
	public void BuildTvSearchQuery_ShouldIncludeCat_WhenCategoriesProvided()
		=> Assert.Equal("?t=tvsearch&apikey=secret&tvdbid=280619&cat=5030,5040",
			_instance.BuildTvSearchQuery(tvdbId: 280619, categories: new[] { 5030, 5040 }));

	[Fact]
	public void BuildMovieSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> Assert.Equal("?t=movie&apikey=secret&q=The%20Matrix&imdbid=tt0133093&tmdbid=603",
			_instance.BuildMovieSearchQuery("The Matrix", "tt0133093", 603));

	[Fact]
	public void BuildMovieSearchQuery_ShouldOmitNullParams_WhenOnlyImdbIdProvided()
		=> Assert.Equal("?t=movie&apikey=secret&imdbid=tt0133093", _instance.BuildMovieSearchQuery(imdbId: "tt0133093"));

	[Fact]
	public void BuildMovieSearchQuery_ShouldIncludeCat_WhenCategoriesProvided()
		=> Assert.Equal("?t=movie&apikey=secret&tmdbid=603&cat=2030,2040",
			_instance.BuildMovieSearchQuery(tmdbId: 603, categories: new[] { 2030, 2040 }));

	[Fact]
	public void ToUri_ShouldCombineBaseUriAndQuery_WhenGivenBaseUri()
		=> Assert.Equal(new Uri("https://indexer.example/api?t=caps&apikey=secret"),
			_instance.ToUri(new Uri("https://indexer.example/api"), _instance.BuildCapsQuery()));
}
