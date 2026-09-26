using System.Net;
using Shouldly;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class TvdbClientTests
{
	[Fact]
	public async Task GetSeriesByTvdb_ShouldLoginOnceAndReuseToken_WhenRequestedTwice()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute)
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);
		await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		handler.Requests.Count(r => r.RequestUri!.PathAndQuery == "/v4/login").ShouldBe(1);
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldReloginOnceAndRetry_WhenUpstreamReturnsUnauthorized()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute)
			.Respond(HttpStatusCode.Unauthorized, """{"status":"failure"}""")
			.Respond(HttpStatusCode.OK, Fixtures.TvdbLoginB)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbSeriesExtended)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesDefault)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesDvd)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesAbsolute)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbTranslations);
		using var host = MetadataTestHost.Create(handler);

		var first = await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);
		var second = await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		const string extendedPath = "/v4/series/75760/extended?meta=episodes&short=true";
		var extendedRequests = handler.Requests
			.Select((request, index) => (request, index))
			.Where(x => x.request.RequestUri!.PathAndQuery == extendedPath)
			.ToList();
		extendedRequests.Count.ShouldBe(3);
		extendedRequests[1].request.Headers.Authorization!.Parameter.ShouldBe("token-a");
		extendedRequests[2].request.Headers.Authorization!.Parameter.ShouldBe("token-b");
		var loginIndexes = handler.RequestPaths
			.Select((path, index) => (path, index))
			.Where(x => x.path == "/v4/login")
			.Select(x => x.index)
			.ToList();
		loginIndexes.Count.ShouldBe(2);
		loginIndexes[1].ShouldBe(extendedRequests[1].index + 1);
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldLoginWithConfiguredApiKey_WhenFirstRequestHappens()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		var login = handler.Requests[0];
		login.Method.ShouldBe(HttpMethod.Post);
		var body = await login.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("tvdb-key");
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldSendPin_WhenConfigured()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler, tvdbPin: "1234");

		await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		var login = handler.Requests[0];
		var body = await login.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("\"pin\":\"1234\"");
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldOmitPin_WhenNotConfigured()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		await host.Tvdb.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		var login = handler.Requests[0];
		var body = await login.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldNotContain("pin");
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldMapSeriesWithAllOrderings_WhenEveryOrderingCoversAllEpisodes()
	{
		var handler = new StubHttpMessageHandler()
			.Respond(HttpStatusCode.OK, Fixtures.TvdbLoginA)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbSeriesExtended.Replace(
				"\"id\":75760",
				"\"id\":75760,\"originalLanguage\":\"sv\"",
				StringComparison.Ordinal))
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesDefault)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesDvd)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbEpisodesAbsolute)
			.Respond(HttpStatusCode.OK, Fixtures.TvdbTranslations);
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.TvdbId.ShouldBe(75760);
		series.TmdbId.ShouldBe(48891);
		series.ImdbId.ShouldBe("tt0836958");
		series.Title.ShouldBe("Wallander");
		series.SortTitle.ShouldBe("wallander");
		series.Status.ShouldBe(SeriesStatus.ENDED);
		series.OriginalLanguage.ShouldBe("sv");
		series.Network.ShouldBe("TV4");
		series.Runtime.ShouldBe(90);
		series.Year.ShouldBe(2005);
		series.FirstAired.ShouldBe(new DateOnly(2005, 9, 18));
		series.Genres.ShouldBe(["Crime", "Drama"]);
		series.PosterUrl.ShouldBe("https://artworks.thetvdb.com/banners/posters/75760-1.jpg");
		series.BackdropUrl.ShouldBe("https://artworks.thetvdb.com/banners/fanart/original/75760-1.jpg");
		series.Seasons.Select(s => (s.SeasonNumber, s.EpisodeCount)).ShouldBe([(1, 2), (2, 1)]);
		series.Episodes.Count.ShouldBe(3);

		series.Episodes[0].TvdbId.ShouldBe(1001);
		series.Episodes[0].AirDate.ShouldBe(new DateOnly(2005, 9, 18));
		series.Episodes[0].ImageUrl.ShouldBe("https://artworks.thetvdb.com/episodes/1001.jpg");
		series.Episodes[0].Numbers.ShouldBe(
		[
			new EpisodeNumber(EpisodeOrdering.AIRED, 1, 1, null),
			new EpisodeNumber(EpisodeOrdering.DVD, 1, 1, null),
			new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 1)
		]);
		series.Episodes[2].Numbers.ShouldBe(
		[
			new EpisodeNumber(EpisodeOrdering.AIRED, 2, 1, null),
			new EpisodeNumber(EpisodeOrdering.DVD, 2, 1, null),
			new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 3)
		]);
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldOmitDvdOrdering_WhenDvdEpisodesOnlyCoverPartOfTheSeries()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvdPartial, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.Episodes.ShouldNotContain(e => e.Numbers.Any(n => n.Ordering == EpisodeOrdering.DVD));
		series.Episodes[2].Numbers.ShouldContain(new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 3));
		series.Episodes[2].Numbers.ShouldContain(new EpisodeNumber(EpisodeOrdering.AIRED, 2, 1, null));
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldOmitAbsoluteNumbersOfZero_WhenAbsoluteOrderingContainsPlaceholders()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsoluteWithZero);
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.Episodes[0].Numbers.ShouldContain(new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 1));
		series.Episodes[1].Numbers.ShouldNotContain(n => n.Ordering == EpisodeOrdering.ABSOLUTE);
		series.Episodes[2].Numbers.ShouldNotContain(n => n.Ordering == EpisodeOrdering.ABSOLUTE);
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldMapAlternateTitlesFromAliasesAndTranslations_WhenBothArePresent()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTvdbAsync(75760, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.AlternateTitles.ShouldContain(new AlternateTitleResource("Kommissar Wallander", "swe"));
		series.AlternateTitles.ShouldContain(new AlternateTitleResource("Kommissar Wallander im fernen Land", "deu"));
		series.AlternateTitles.ShouldNotContain(new AlternateTitleResource("Wallander", "swe"));
	}
}
