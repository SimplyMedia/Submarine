using System.Net;
using Shouldly;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class TmdbMovieTests
{
	[Fact]
	public async Task GetMovie_ShouldMapReleaseDatesPerType_WhenReleaseDatesExist()
	{
		var detail = Fixtures.TmdbMovieDetail.Replace(
			"\"id\":27205",
			"\"id\":27205,\"original_language\":\"en\",\"keywords\":{\"keywords\":[{\"id\":1,\"name\":\"dreams\"},{\"id\":2,\"name\":\"heist\"}]}",
			StringComparison.Ordinal);
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, detail);
		using var host = MetadataTestHost.Create(handler);

		var movie = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		movie.ShouldNotBeNull();
		movie.TmdbId.ShouldBe(27205);
		movie.ImdbId.ShouldBe("tt1375666");
		movie.Title.ShouldBe("Inception");
		movie.OriginalTitle.ShouldBe("Inception");
		movie.InCinemasDate.ShouldBe(new DateOnly(2010, 7, 16));
		movie.DigitalReleaseDate.ShouldBe(new DateOnly(2017, 12, 19));
		movie.PhysicalReleaseDate.ShouldBe(new DateOnly(2010, 12, 7));
		movie.Year.ShouldBe(2010);
		movie.Runtime.ShouldBe(148);
		movie.PosterUrl.ShouldBe("https://image.tmdb.org/t/p/original/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg");
		movie.OriginalLanguage.ShouldBe("en");
		movie.Keywords.ShouldBe(["dreams", "heist"]);
		handler.RequestPaths[0].ShouldContain("append_to_response=release_dates,alternative_titles,videos,external_ids,keywords");
	}

	[Fact]
	public async Task GetMovieList_ShouldMapOriginalLanguageFromSummary_WhenPresent()
	{
		var handler = new StubHttpMessageHandler().Respond(
			HttpStatusCode.OK,
			"""{"id":1,"name":"List","overview":null,"items":[{"id":27205,"title":"Inception","original_title":"Inception","original_language":"en"}]}""");
		using var host = MetadataTestHost.Create(handler);

		var movies = await host.Service.GetMovieListAsync(1, TestContext.Current.CancellationToken);

		movies.ShouldHaveSingleItem().OriginalLanguage.ShouldBe("en");
		movies[0].Keywords.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetMovie_ShouldDeriveReleasedStatus_WhenDigitalOrPhysicalDateHasPassed()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		// Default clock: 2026-09-26, digital (2017) and physical (2010) long out.
		using var host = MetadataTestHost.Create(handler);

		var movie = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		movie!.Status.ShouldBe(MovieStatus.RELEASED);
	}

	[Fact]
	public async Task GetMovie_ShouldDeriveInCinemasStatus_WhenOnlyTheTheatricalDateHasPassed()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler, new DateTimeOffset(2010, 8, 1, 0, 0, 0, TimeSpan.Zero));

		var movie = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		movie!.Status.ShouldBe(MovieStatus.IN_CINEMAS);
	}

	[Fact]
	public async Task GetMovie_ShouldDeriveAnnouncedStatus_WhenEveryReleaseDateLiesAhead()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler, new DateTimeOffset(2010, 6, 1, 0, 0, 0, TimeSpan.Zero));

		var movie = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		movie!.Status.ShouldBe(MovieStatus.ANNOUNCED);
	}

	[Fact]
	public async Task GetMovie_ShouldMapStudioCertificationTrailerCollectionAndAlternateTitles_WhenPresent()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler);

		var movie = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		movie.ShouldNotBeNull();
		movie.Studio.ShouldBe("Syncopy");
		movie.Certification.ShouldBe("PG-13");
		movie.YouTubeTrailerId.ShouldBe("YoHD9XEInc0");
		movie.TmdbCollectionId.ShouldBe(525);
		movie.CollectionTitle.ShouldBe("Inception Collection");
		movie.AlternateTitles.ShouldHaveSingleItem();
		movie.AlternateTitles[0].ShouldBe(new AlternateTitleResource("Origen", "ES"));
	}

	[Fact]
	public async Task GetMovieByImdb_ShouldResolveThroughFindEndpoint_WhenImdbIdIsKnown()
	{
		var handler = new StubHttpMessageHandler()
			.Respond(HttpStatusCode.OK, Fixtures.TmdbFind)
			.Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler);

		var movie = await host.Service.GetMovieByImdbAsync("tt1375666", TestContext.Current.CancellationToken);

		movie.ShouldNotBeNull();
		movie.TmdbId.ShouldBe(27205);
		handler.RequestPaths[0].ShouldBe("/3/find/tt1375666?external_source=imdb_id");
		handler.RequestPaths[1].ShouldStartWith("/3/movie/27205");
	}
}
