using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatLanguageProfileTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatLanguageProfileTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SonarrLanguageProfiles_ShouldUseServarrNestedLanguageShape()
	{
		using var anonymous = _factory.CreateClient();
		using (var unauthorized = await anonymous.GetAsync("/compat/sonarr/api/v3/languageprofile"))
			unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		using var client = await _factory.CreateAuthorizedClientAsync();
		using var response = await client.GetAsync("/compat/sonarr/api/v3/languageprofile");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
		var profile = document.RootElement.EnumerateArray().First();
		profile.GetProperty("id").GetInt32().ShouldBeGreaterThan(0);
		profile.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
		profile.GetProperty("upgradeAllowed").ValueKind.ShouldBe(JsonValueKind.True);
		profile.GetProperty("cutoff").GetProperty("id").GetInt32().ShouldBe(1);
		profile.GetProperty("languages").ValueKind.ShouldBe(JsonValueKind.Array);
		var english = profile.GetProperty("languages").EnumerateArray().First();
		english.GetProperty("language").GetProperty("id").GetInt32().ShouldBe(1);
		english.GetProperty("language").GetProperty("name").GetString().ShouldBe("English");
		english.GetProperty("allowed").GetBoolean().ShouldBeTrue();

		using var detail = await client.GetAsync($"/compat/sonarr/api/v3/languageprofile/{profile.GetProperty("id").GetInt32()}");
		detail.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
