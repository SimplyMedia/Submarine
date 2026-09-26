using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class GeneralProxySettingsIntegrationTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public GeneralProxySettingsIntegrationTests(SubmarineApiFactory factory) => _factory = factory;


	[Fact]
	public async Task GeneralSettings_ShouldRejectInvalidOutboundProxySettings()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await client.GetStringAsync("/api/v1/config/general"))!;

		var missingHost = original.DeepClone();
		missingHost["proxyEnabled"] = true;
		missingHost["proxyHost"] = "";
		using var missingHostResponse = await client.PutAsync("/api/v1/config/general", JsonContent(missingHost));
		missingHostResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var invalidPort = original.DeepClone();
		invalidPort["proxyEnabled"] = true;
		invalidPort["proxyHost"] = "proxy.example";
		invalidPort["proxyPort"] = 0;
		using var invalidPortResponse = await client.PutAsync("/api/v1/config/general", JsonContent(invalidPort));
		invalidPortResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static StringContent JsonContent(JsonNode value)
		=> new(value.ToJsonString(), Encoding.UTF8, "application/json");
}
