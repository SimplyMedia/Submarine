using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class MetadataEndpointsTests
{
	[Fact]
	public async Task GetMovieSearch_ShouldReturnMappedResults_WhenUpstreamReturnsResults()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieSearch);
		using var factory = new MetadataFactory(handler);

		var response = await factory.CreateClient().GetAsync("/api/v1/movie/search?term=inception", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var results = await response.Content.ReadFromJsonAsync<List<SearchResultResource>>(TestContext.Current.CancellationToken);
		results.ShouldNotBeNull();
		results.ShouldHaveSingleItem().TmdbId.ShouldBe(27205);
	}

	[Fact]
	public async Task GetMovie_ShouldReturn404Problem_WhenUpstreamReturnsNotFound()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.NotFound, "{}");
		using var factory = new MetadataFactory(handler);

		var response = await factory.CreateClient().GetAsync("/api/v1/movie/99999999", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("Not found");
	}

	[Fact]
	public async Task GetMovie_ShouldReturn502ProblemWithUpstreamStatus_WhenUpstreamFails()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.InternalServerError, "{}");
		using var factory = new MetadataFactory(handler);

		var response = await factory.CreateClient().GetAsync("/api/v1/movie/27205", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("\"status\":502");
		body.ShouldContain("\"upstreamStatus\":500");
	}

	[Fact]
	public async Task Get_ShouldRequireApiKeyHeader_WhenApiKeyIsConfigured()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, Fixtures.TmdbMovieSearch);
		using var factory = new MetadataFactory(handler, new Dictionary<string, string?> { ["Auth:ApiKey"] = "secret" });
		var client = factory.CreateClient();

		var denied = await client.GetAsync("/api/v1/movie/search?term=inception", TestContext.Current.CancellationToken);

		denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		client.DefaultRequestHeaders.Add("X-Api-Key", "secret");
		var allowed = await client.GetAsync("/api/v1/movie/search?term=inception", TestContext.Current.CancellationToken);

		allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GetHealthz_ShouldReturnOk()
	{
		using var factory = new MetadataFactory(new StubHttpMessageHandler());

		var response = await factory.CreateClient().GetAsync("/_status/healthz", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("\"status\":\"ok\"");
	}

	[Fact]
	public async Task GetMovie_ShouldNotLeakApiKeyOrQueryString_WhenUpstreamFailsWithApiKeyConfigured()
	{
		var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.InternalServerError, "{}");
		using var factory = new MetadataFactory(handler, new Dictionary<string, string?> { ["Tmdb:ApiKey"] = "super-secret" });

		var response = await factory.CreateClient().GetAsync("/api/v1/movie/27205", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldNotContain("super-secret");
		body.ShouldNotContain("?");
		body.ShouldContain("TMDB request failed with status 500.");
	}

	[Fact]
	public async Task GetReady_ShouldReportConfiguredProviders()
	{
		using var factory = new MetadataFactory(
			new StubHttpMessageHandler(),
			new Dictionary<string, string?> { ["Tmdb:ApiKey"] = "secret", ["Tvdb:ApiKey"] = "" });

		var response = await factory.CreateClient().GetAsync("/_status/ready", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("\"tmdb\":true");
		body.ShouldContain("\"tvdb\":false");
	}

	[Fact]
	public async Task GetReady_ShouldBeAnonymous_WhenApiKeyIsConfigured()
	{
		using var factory = new MetadataFactory(
			new StubHttpMessageHandler(),
			new Dictionary<string, string?> { ["Auth:ApiKey"] = "secret" });

		var response = await factory.CreateClient().GetAsync("/_status/ready", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GetSeriesByTvdb_ShouldSerializeOrderingAndStatusAsStrings()
	{
		var handler = new StubHttpMessageHandler()
			.EnqueueTvdbSeries(Fixtures.TvdbEpisodesDvd, Fixtures.TvdbEpisodesAbsolute);
		using var factory = new MetadataFactory(handler);

		var response = await factory.CreateClient().GetAsync("/api/v1/series/tvdb/75760", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		body.ShouldContain("\"ordering\":\"AIRED\"");
		body.ShouldContain("\"ordering\":\"DVD\"");
		body.ShouldContain("\"ordering\":\"ABSOLUTE\"");
		body.ShouldContain("\"status\":\"ENDED\"");
	}
}
