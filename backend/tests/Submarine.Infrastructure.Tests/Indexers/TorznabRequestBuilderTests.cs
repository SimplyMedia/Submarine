using System;
using System.Collections.Generic;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Indexers.Torznab;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Indexers;

public class TorznabRequestBuilderTests
{
	private readonly TorznabRequestBuilder _instance = new("secret");

	[Fact]
	public void BuildCapsQuery_ShouldIncludeApiKey_WhenApiKeyProvided()
		=> _instance.BuildCapsQuery().ShouldBe("?t=caps&apikey=secret");

	[Fact]
	public void BuildCapsQuery_ShouldOmitApiKey_WhenApiKeyMissing()
		=> new TorznabRequestBuilder(null).BuildCapsQuery().ShouldBe("?t=caps");

	[Fact]
	public void BuildSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> new TorznabRequestBuilder("secret").BuildSearchQuery(new BasicSearchRequest("ubuntu", [5030, 5040], 100, 50))
			.ShouldBe("?t=search&apikey=secret&q=ubuntu&cat=5030,5040&limit=100&offset=50");

	[Fact]
	public void BuildSearchQuery_ShouldOmitNullParams_WhenOnlyQueryProvided()
		=> _instance.BuildSearchQuery(new BasicSearchRequest("ubuntu"))
			.ShouldBe("?t=search&apikey=secret&q=ubuntu");

	[Fact]
	public void BuildSearchQuery_ShouldOmitCat_WhenCategoriesEmpty()
		=> _instance.BuildSearchQuery(new BasicSearchRequest())
			.ShouldBe("?t=search&apikey=secret");

	[Theory]
	[InlineData("Ubuntu 22.04 & More", "Ubuntu%2022.04%20%26%20More")]
	[InlineData("C#/.NET?", "C%23%2F.NET%3F")]
	public void BuildSearchQuery_ShouldEscapeQuery_WhenQueryContainsReservedChars(string query, string expected)
		=> _instance.BuildSearchQuery(new BasicSearchRequest(query))
			.ShouldBe($"?t=search&apikey=secret&q={expected}");

	[Fact]
	public void BuildTvSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> _instance.BuildSearchQuery(new TvSearchRequest("The Expanse", 2, 5, 280619, null, "tt3230854"))
			.ShouldBe("?t=tvsearch&apikey=secret&q=The%20Expanse&season=2&ep=5&tvdbid=280619&imdbid=3230854");

	[Fact]
	public void BuildTvSearchQuery_ShouldStripTtPrefix_WhenImdbIdHasPrefix()
		=> _instance.BuildSearchQuery(new TvSearchRequest(ImdbId: "tt3230854"))
			.ShouldBe("?t=tvsearch&apikey=secret&imdbid=3230854");

	[Fact]
	public void BuildTvSearchQuery_ShouldIncludeTmdbId_WhenRequested()
		=> _instance.BuildSearchQuery(new TvSearchRequest(TmdbId: 280619))
			.ShouldBe("?t=tvsearch&apikey=secret&tmdbid=280619");

	[Fact]
	public void BuildTvSearchQuery_ShouldIncludeCat_WhenCategoriesProvided()
		=> _instance.BuildSearchQuery(new TvSearchRequest(TvdbId: 280619, Categories: [5030, 5040]))
			.ShouldBe("?t=tvsearch&apikey=secret&tvdbid=280619&cat=5030,5040");

	[Fact]
	public void BuildMovieSearchQuery_ShouldIncludeAllParams_WhenAllProvided()
		=> _instance.BuildSearchQuery(new MovieSearchRequest("The Matrix", 1999, "tt0133093", 603))
			.ShouldBe("?t=movie&apikey=secret&q=The%20Matrix&imdbid=0133093&tmdbid=603&year=1999");

	[Fact]
	public void BuildMovieSearchQuery_ShouldOmitNullParams_WhenOnlyImdbIdProvided()
		=> _instance.BuildSearchQuery(new MovieSearchRequest(ImdbId: "tt0133093"))
			.ShouldBe("?t=movie&apikey=secret&imdbid=0133093");

	[Fact]
	public void BuildMovieSearchQuery_ShouldIncludeCat_WhenCategoriesProvided()
		=> _instance.BuildSearchQuery(new MovieSearchRequest(TmdbId: 603, Categories: [2030, 2040]))
			.ShouldBe("?t=movie&apikey=secret&tmdbid=603&cat=2030,2040");

	[Fact]
	public void BuildSearchQuery_ShouldAppendAdditionalParameters_WhenProvided()
		=> _instance.BuildSearchQuery(new BasicSearchRequest("ubuntu"), "&extended=1&maxage=100")
			.ShouldBe("?t=search&apikey=secret&q=ubuntu&extended=1&maxage=100");

	[Fact]
	public void ToUri_ShouldCombineBaseUrlApiPathAndQuery_WhenRequested()
		=> TorznabRequestBuilder.ToUri("https://indexer.example", "/api", "?t=caps&apikey=secret")
			.ShouldBe(new Uri("https://indexer.example/api?t=caps&apikey=secret"));

	[Fact]
	public void ToUri_ShouldKeepPathOfBaseUrl_WhenBaseUrlHasSubdirectory()
		=> TorznabRequestBuilder.ToUri("https://indexer.example/torznab", "api", "?t=caps")
			.ShouldBe(new Uri("https://indexer.example/torznab/api?t=caps"));
}
