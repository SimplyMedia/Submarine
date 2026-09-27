using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatSonarrRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatSonarrRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SeriesAndEpisodeCollections_RequireApiKey_AndReturnSonarrArrays()
	{
		using var anonymous = _factory.CreateClient();
		using var rejected = await anonymous.GetAsync("/compat/sonarr/api/v3/series");
		rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await rejected.Content.ReadAsStringAsync()).ShouldBeEmpty();

		using var client = await _factory.CreateAuthorizedClientAsync();
		using var seriesResponse = await client.GetAsync("/compat/sonarr/api/v3/series");
		seriesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var series = await seriesResponse.Content.ReadFromJsonAsync<JsonElement>();
		series.ValueKind.ShouldBe(JsonValueKind.Array);
		using var episodesResponse = await client.GetAsync("/compat/sonarr/api/v3/episode");
		episodesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await episodesResponse.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.ShouldBe(JsonValueKind.Array);
	}

	[Fact]
	public async Task UnknownSonarrResource_ReturnsJson404InsteadOfSpaDocument()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		using var response = await client.GetAsync("/compat/sonarr/api/v3/not-a-resource");
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
	}
}
