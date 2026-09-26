using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatRuntimeRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatRuntimeRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task FacadeDiscoveryAndStatus_ShouldUsePinnedDialectsAndPathBase()
	{
		using var admin = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await admin.GetStringAsync("/api/v1/config/general"))!;
		try
		{
			var updated = original.DeepClone();
			updated["urlBase"] = "/submarine";
			using (var write = await admin.PutAsync("/api/v1/config/general", JsonContent(updated)))
				write.StatusCode.ShouldBe(HttpStatusCode.OK);

			using var client = _factory.CreateClient();
			foreach (var (facade, version, appName, apiVersion) in new[]
			{
				("sonarr", "4.0.20.3014", "Sonarr", "v3"),
				("radarr", "6.4.4.10685", "Radarr", "v3"),
				("prowlarr", "2.6.5.5623", "Prowlarr", "v1")
			})
			{
				using var discoveryResponse = await client.GetAsync($"/submarine/compat/{facade}/api?apikey={Uri.EscapeDataString(_factory.ReadApiKey())}");
				discoveryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
				using var discovery = JsonDocument.Parse(await discoveryResponse.Content.ReadAsStringAsync());
				discovery.RootElement.GetProperty("current").GetString().ShouldBe(apiVersion);
				discovery.RootElement.GetProperty("deprecated").GetArrayLength().ShouldBe(0);

				using var statusResponse = await client.GetAsync($"/submarine/compat/{facade}/api/{apiVersion}/system/status?apikey={Uri.EscapeDataString(_factory.ReadApiKey())}");
				statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
				using var status = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
				var root = status.RootElement;
				root.GetProperty("appName").GetString().ShouldBe(appName);
				root.GetProperty("version").GetString().ShouldBe(version);
				root.GetProperty("submarineVersion").GetString().ShouldNotBeNullOrWhiteSpace();
				root.GetProperty("urlBase").GetString().ShouldBe("/submarine");
				root.GetProperty("startTime").ValueKind.ShouldBe(JsonValueKind.String);
				root.GetProperty("databaseType").GetString().ShouldNotBeNullOrWhiteSpace();
				root.GetProperty("runtimeName").GetString().ShouldNotBeNullOrWhiteSpace();
			}

			using var statusAlias = await client.GetAsync("/submarine/compat/sonarr/api/system/status?apikey=" + Uri.EscapeDataString(_factory.ReadApiKey()));
			statusAlias.StatusCode.ShouldBe(HttpStatusCode.OK);

			using var notFound = await client.GetAsync("/submarine/compat/sonarr/api/v3/does-not-exist?apikey=" + Uri.EscapeDataString(_factory.ReadApiKey()));
			notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
			notFound.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
			(await notFound.Content.ReadAsStringAsync()).ShouldNotContain("<html", Case.Sensitive);

			using var unauthorizedUnknown = await client.GetAsync("/submarine/compat/sonarr/api/v3/does-not-exist");
			unauthorizedUnknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await unauthorizedUnknown.Content.ReadAsStringAsync()).ShouldBeEmpty();

			using var wrongMethod = await client.PostAsync("/submarine/compat/sonarr/api?apikey=" + Uri.EscapeDataString(_factory.ReadApiKey()), null);
			wrongMethod.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
		}
		finally
		{
			using var restore = await admin.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	private static StringContent JsonContent(JsonNode node)
		=> new(node.ToJsonString(), Encoding.UTF8, "application/json");
}
