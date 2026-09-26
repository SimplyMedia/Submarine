using Shouldly;
using Submarine.Core.Indexers;
using Submarine.Core.Search;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class SearchRequestBuilderTests
{
	[Fact]
	public void BuildSeriesQuery_ShouldPopulateIdsAndTitle()
	{
		var request = SearchRequestBuilder.BuildSeriesQuery("The Show", 123, 456, "tt789");

		request.Query.ShouldBe("The Show");
		request.TvdbId.ShouldBe(123);
		request.TmdbId.ShouldBe(456);
		request.ImdbId.ShouldBe("tt789");
		request.Season.ShouldBeNull();
		request.Episode.ShouldBeNull();
	}

	[Fact]
	public void BuildSeasonQuery_ShouldEmbedSeasonInQueryAndField()
	{
		var request = SearchRequestBuilder.BuildSeasonQuery("The Show", 3, 1, null, null);

		request.Query.ShouldBe("The Show S03");
		request.Season.ShouldBe(3);
		request.Episode.ShouldBeNull();
	}

	[Fact]
	public void BuildStandardEpisodeQuery_ShouldEmbedSeasonAndEpisode()
	{
		var request = SearchRequestBuilder.BuildStandardEpisodeQuery("The Show", 2, 5, 1, null, null);

		request.Query.ShouldBe("The Show S02E05");
		request.Season.ShouldBe(2);
		request.Episode.ShouldBe(5);
	}

	[Fact]
	public void BuildDailyEpisodeQuery_ShouldFormatDateAsDotSeparated()
	{
		var request = SearchRequestBuilder.BuildDailyEpisodeQuery("Talk Show", new DateTime(2024, 3, 7), null, null, null);

		request.Query.ShouldBe("Talk Show 2024.03.07");
		request.Season.ShouldBeNull();
	}

	[Fact]
	public void BuildAnimeEpisodeQueries_ShouldReturnOnlyAbsoluteForm_WhenStandardFormatSearchDisabled()
	{
		var requests = SearchRequestBuilder.BuildAnimeEpisodeQueries("Anime", 1, 5, 5, null, null, null, animeStandardFormatSearch: false);

		requests.Count.ShouldBe(1);
		requests[0].Query.ShouldBe("Anime 005");
		requests[0].AbsoluteEpisode.ShouldBe(5);
	}

	[Fact]
	public void BuildAnimeEpisodeQueries_ShouldReturnBothForms_WhenStandardFormatSearchEnabled()
	{
		var requests = SearchRequestBuilder.BuildAnimeEpisodeQueries("Anime", 1, 5, 5, null, null, null, animeStandardFormatSearch: true);

		requests.Count.ShouldBe(2);
		requests[0].Query.ShouldBe("Anime 005");
		requests[1].Query.ShouldBe("Anime S01E05");
		requests[1].Season.ShouldBe(1);
		requests[1].Episode.ShouldBe(5);
	}

	[Fact]
	public void BuildMovieQuery_ShouldPopulateYearAndIds()
	{
		var request = SearchRequestBuilder.BuildMovieQuery("A Movie", 2020, "tt1", 42);

		request.Query.ShouldBe("A Movie");
		request.Year.ShouldBe(2020);
		request.ImdbId.ShouldBe("tt1");
		request.TmdbId.ShouldBe(42);
	}

	[Fact]
	public void BuildTextQuery_ShouldPassThroughTermAndCategories()
	{
		var request = SearchRequestBuilder.BuildTextQuery("some term", [5000]);

		request.Query.ShouldBe("some term");
		request.Categories.ShouldBe([5000]);
	}
}
