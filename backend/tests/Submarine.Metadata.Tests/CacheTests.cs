using System.Net;
using Shouldly;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class CacheTests
{
	[Fact]
	public async Task GetMovie_ShouldHitUpstreamOnlyOnce_WhenSameMovieIsRequestedTwice()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieDetail);
		using var host = MetadataTestHost.Create(handler);

		var first = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);
		var second = await host.Service.GetMovieAsync(27205, TestContext.Current.CancellationToken);

		first.ShouldNotBeNull();
		second!.TmdbId.ShouldBe(first.TmdbId);
		handler.Requests.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task SearchMovies_ShouldHitUpstreamOnlyOnce_WhenSameTermIsRequestedTwice()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieSearch);
		using var host = MetadataTestHost.Create(handler);

		var first = await host.Service.SearchMoviesAsync("inception", null, TestContext.Current.CancellationToken);
		var second = await host.Service.SearchMoviesAsync("inception", null, TestContext.Current.CancellationToken);

		first.ShouldHaveSingleItem();
		second.ShouldHaveSingleItem();
		handler.Requests.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldHitUpstreamOnlyOnce_WhenSameSeriesIsRequestedTwice()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		var first = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);
		var second = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		first.ShouldNotBeNull();
		second!.TvdbId.ShouldBe(first.TvdbId);
		handler.Requests.Count.ShouldBe(6);
	}
}
