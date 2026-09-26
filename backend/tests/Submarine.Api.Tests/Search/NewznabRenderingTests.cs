using Shouldly;
using Submarine.Api.Features.Newznab;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.Tests.Search;

public sealed class NewznabRenderingTests
{
	[Fact]
	public void BuildError_ShouldRenderCodeAndDescription()
	{
		var document = NewznabModule.BuildError(100, "Invalid API Key");

		document.Root!.Name.LocalName.ShouldBe("error");
		document.Root.Attribute("code")!.Value.ShouldBe("100");
		document.Root.Attribute("description")!.Value.ShouldBe("Invalid API Key");
	}

	[Fact]
	public void BuildCaps_ShouldReflectAggregatedAvailabilityAndCategories()
	{
		var capabilities = new List<IndexerCapabilities>
		{
			new() { SearchAvailable = true, TvSearchAvailable = false, MovieSearchAvailable = true, Categories = [new IndexerCategory(5000, "TV", [])] }
		};

		var document = NewznabModule.BuildCaps(capabilities);

		var searching = document.Root!.Element("searching")!;
		searching.Element("search")!.Attribute("available")!.Value.ShouldBe("yes");
		searching.Element("tv-search")!.Attribute("available")!.Value.ShouldBe("no");
		searching.Element("movie-search")!.Attribute("available")!.Value.ShouldBe("yes");

		var categories = document.Root.Element("categories")!.Elements("category").ToList();
		categories.Count.ShouldBe(1);
		categories[0].Attribute("id")!.Value.ShouldBe("5000");
	}

	[Fact]
	public void BuildCaps_ShouldFallBackToStandardCategoryTree_WhenNoIndexerReportsAny()
	{
		var document = NewznabModule.BuildCaps([new IndexerCapabilities()]);

		var categories = document.Root!.Element("categories")!.Elements("category").ToList();
		categories.Count.ShouldBe(IndexerCategories.All.Count);
	}

	[Fact]
	public void BuildFeed_ShouldRenderTorznabAttributesAndNoEnclosure_WhenIndexerIdMissing()
	{
		var release = new ReleaseInfo
		{
			Guid = "guid-1",
			Title = "Show.S01E01.1080p",
			Size = 1000,
			Seeders = 10,
			Leechers = 2,
			TvdbId = 42,
			Protocol = Protocol.BITTORRENT,
			MagnetUrl = "magnet:?xt=urn:btih:abc"
		};

		var document = NewznabModule.BuildFeed([release], "key");

		var item = document.Descendants("item").Single();
		item.Element("title")!.Value.ShouldBe("Show.S01E01.1080p");
		item.Element("guid")!.Value.ShouldBe("guid-1");
		item.Element("enclosure").ShouldBeNull();

		var attrs = item.Elements().Where(element => element.Name.LocalName == "attr").ToList();
		attrs.ShouldContain(attr => attr.Attribute("name")!.Value == "seeders" && attr.Attribute("value")!.Value == "10");
		attrs.ShouldContain(attr => attr.Attribute("name")!.Value == "tvdbid" && attr.Attribute("value")!.Value == "42");
		attrs.ShouldContain(attr => attr.Attribute("name")!.Value == "magneturl");
	}

	[Fact]
	public void BuildFeed_ShouldRenderEnclosureThroughDownloadProxy_WhenIndexerIdSet()
	{
		var release = new ReleaseInfo
		{
			Guid = "guid-2",
			Title = "Movie 2020",
			Size = 500,
			Protocol = Protocol.BITTORRENT,
			MagnetUrl = "magnet:?xt=urn:btih:xyz",
			IndexerId = 7
		};

		var document = NewznabModule.BuildFeed([release], "my-api-key");

		var enclosure = document.Descendants("enclosure").Single();
		var url = enclosure.Attribute("url")!.Value;
		url.ShouldStartWith("/api/v1/indexer/7/download?link=");
		url.ShouldContain("apikey=my-api-key");
		enclosure.Attribute("length")!.Value.ShouldBe("500");
	}

	[Fact]
	public void BuildRequest_ShouldBuildTvSearchRequest_WhenTypeIsTvsearch()
	{
		var query = new ReleaseSearchQuery("tvsearch", "show", 1, 2, 100, 200, "tt1", "5000,5040", 50, 0, "key");

		var request = NewznabModule.BuildRequest(query).ShouldBeOfType<TvSearchRequest>();

		request.Query.ShouldBe("show");
		request.Season.ShouldBe(1);
		request.Episode.ShouldBe(2);
		request.TvdbId.ShouldBe(100);
		request.TmdbId.ShouldBe(200);
		request.ImdbId.ShouldBe("tt1");
		request.Categories.ShouldBe([5000, 5040]);
	}

	[Fact]
	public void BuildRequest_ShouldBuildMovieSearchRequest_WhenTypeIsMovie()
	{
		var query = new ReleaseSearchQuery("movie", "a movie", null, null, null, 55, "tt9", null, null, null, "key");

		var request = NewznabModule.BuildRequest(query).ShouldBeOfType<MovieSearchRequest>();

		request.Query.ShouldBe("a movie");
		request.TmdbId.ShouldBe(55);
		request.ImdbId.ShouldBe("tt9");
	}

	[Fact]
	public void BuildRequest_ShouldBuildBasicSearchRequest_ForSearchOrUnknownType()
	{
		var query = new ReleaseSearchQuery("search", "term", null, null, null, null, null, null, null, null, "key");

		var request = NewznabModule.BuildRequest(query).ShouldBeOfType<BasicSearchRequest>();

		request.Query.ShouldBe("term");
	}
}
