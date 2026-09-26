using System.Net;
using Shouldly;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class SearchTests
{
	[Fact]
	public async Task SearchSeries_ShouldLookupTvdbDetail_WhenTermHasTvdbPrefix()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchSeriesAsync("tvdb:75760", null, TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].TvdbId.ShouldBe(75760);
		results[0].Provider.ShouldBe("tvdb");
		results[0].Status.ShouldBe("ENDED");
		results[0].Title.ShouldBe("Wallander");
		results[0].Year.ShouldBe(2005);
		handler.RequestPaths.ShouldNotContain(path => path.Contains("search"));
	}

	[Fact]
	public async Task SearchSeries_ShouldLookupTmdbDetail_WhenTermHasTmdbPrefix()
	{
		var handler = new StubHttpMessageHandler().EnqueueTmdbSeriesWithoutGroups();
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchSeriesAsync("tmdb:1399", null, TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].Provider.ShouldBe("tmdb");
		results[0].TmdbId.ShouldBe(1399);
		results[0].Title.ShouldBe("Game of Thrones");
	}

	[Fact]
	public async Task SearchSeries_ShouldSearchTvdbByDefault_WhenPlainTermIsGiven()
	{
		var handler = new StubHttpMessageHandler()
			.Respond(HttpStatusCode.OK, Fixtures.TvdbLoginA)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbSearch);
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchSeriesAsync("wallander", null, TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].TvdbId.ShouldBe(75760);
		results[0].Status.ShouldBe("Ended");
		handler.RequestPaths[1].ShouldBe("/v4/search?query=wallander&type=series");
	}

	[Fact]
	public async Task SearchSeries_ShouldSearchTmdb_WhenProviderParameterIsTmdb()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbSeriesSearch);
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchSeriesAsync("game of thrones", "tmdb", TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].Provider.ShouldBe("tmdb");
		results[0].TmdbId.ShouldBe(1399);
		handler.RequestPaths[0].ShouldBe("/3/search/tv?query=game%20of%20thrones");
	}

	[Fact]
	public async Task SearchMovies_ShouldLookupMovieDetail_WhenTermHasTmdbPrefix()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchMoviesAsync("tmdb:27205", null, TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].Provider.ShouldBe("tmdb");
		results[0].TmdbId.ShouldBe(27205);
		results[0].Status.ShouldBe("RELEASED");
		handler.RequestPaths.ShouldNotContain(path => path.Contains("search"));
	}

	[Fact]
	public async Task SearchMovies_ShouldResolveThroughTmdbFind_WhenTermHasImdbPrefix()
	{
		var handler = new StubHttpMessageHandler()
			.Respond(HttpStatusCode.OK, Fixtures.TmdbFind)
			.Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler);

		var results = await host.Service.SearchMoviesAsync("imdb:tt1375666", null, TestContext.Current.CancellationToken);

		results.ShouldHaveSingleItem();
		results[0].TmdbId.ShouldBe(27205);
		results[0].ImdbId.ShouldBe("tt1375666");
		handler.RequestPaths[0].ShouldBe("/3/find/tt1375666?external_source=imdb_id");
	}

	[Fact]
	public async Task SearchMovies_ShouldPassYearToUpstream_WhenYearIsGiven()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieSearch);
		using var host = MetadataTestHost.Create(handler);

		await host.Service.SearchMoviesAsync("inception", 2010, TestContext.Current.CancellationToken);

		handler.RequestPaths[0].ShouldBe("/3/search/movie?query=inception&year=2010");
	}
}
