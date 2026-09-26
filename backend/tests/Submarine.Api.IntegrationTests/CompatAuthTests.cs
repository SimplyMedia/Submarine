using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatAuthTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatAuthTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task Facades_ShouldRequireOnlyGlobalApiKeyRegardlessOfUiAuthMode()
	{
		using var admin = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await admin.GetStringAsync("/api/v1/config/general"))!;
		try
		{
			foreach (var authMethod in new[] { "NONE", "FORMS", "BASIC", "EXTERNAL" })
			{
				await SetAuthAsync(admin, original, authMethod, "ENABLED");
				await AssertCredentialsAsync(_factory.ReadApiKey());
			}

			await SetAuthAsync(admin, original, "FORMS", "DISABLED_FOR_LOCAL_ADDRESSES");
			await AssertCredentialsAsync(_factory.ReadApiKey());
			await AssertValidCookieIsNotCompatAuthorizationAsync();
		}
		finally
		{
			using var restore = await admin.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	[Fact]
	public async Task CompatApiKey_ShouldHonorRotationAndScopedSignalRAccessToken()
	{
		using var admin = await _factory.CreateAuthorizedClientAsync();
		var oldKey = _factory.ReadApiKey();
		using var anonymous = _factory.CreateClient();
		using (var negotiate = await anonymous.PostAsync(
			"/compat/sonarr/signalr/messages/negotiate?negotiateVersion=1&access_token=" + Uri.EscapeDataString(oldKey), null))
			negotiate.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var restToken = await anonymous.GetAsync(
			"/compat/sonarr/api/v3/system/status?access_token=" + Uri.EscapeDataString(oldKey)))
		{
			restToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await restToken.Content.ReadAsStringAsync()).ShouldBeEmpty();
		}

		using var rotated = await admin.PostAsync("/api/v1/config/general/api-key", null);
		rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
		var newKey = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync()).RootElement.GetProperty("apiKey").GetString();
		newKey.ShouldNotBeNullOrWhiteSpace();
		using (var old = await anonymous.GetAsync("/compat/sonarr/api?apikey=" + Uri.EscapeDataString(oldKey)))
		{
			old.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await old.Content.ReadAsStringAsync()).ShouldBeEmpty();
		}

		using var fresh = _factory.CreateClient();
		fresh.DefaultRequestHeaders.Add("X-Api-Key", newKey);
		using var accepted = await fresh.GetAsync("/compat/sonarr/api");
		accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var misplacedToken = await fresh.GetAsync("/compat/sonarr/api?access_token=" + Uri.EscapeDataString(newKey!));
		misplacedToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await misplacedToken.Content.ReadAsStringAsync()).ShouldBeEmpty();
	}

	private async Task AssertCredentialsAsync(string key)
	{
		using var anonymous = _factory.CreateClient();
		foreach (var route in new[] { "/compat/sonarr/api", "/compat/radarr/api", "/compat/prowlarr/api" })
		{
			using var missing = await anonymous.GetAsync(route);
			missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await missing.Content.ReadAsStringAsync()).ShouldBeEmpty();
		}

		using (var query = await anonymous.GetAsync("/compat/sonarr/api?apiKey=" + Uri.EscapeDataString(key)))
			query.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var invalidHeaderAndValidQuery = await SendAsync(anonymous, "/compat/sonarr/api?apikey=" + Uri.EscapeDataString(key), "wrong-key"))
		{
			invalidHeaderAndValidQuery.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await invalidHeaderAndValidQuery.Content.ReadAsStringAsync()).ShouldBeEmpty();
		}

		using (var matchingCredentials = await SendAsync(anonymous, "/compat/radarr/api?apikey=" + Uri.EscapeDataString(key), key))
			matchingCredentials.StatusCode.ShouldBe(HttpStatusCode.OK);

		using (var invalid = await SendAsync(anonymous, "/compat/sonarr/api", "wrong-key"))
			invalid.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		using (var repeatedQuery = await anonymous.GetAsync(
			"/compat/sonarr/api?apikey=" + Uri.EscapeDataString(key) + "&apiKey=" + Uri.EscapeDataString(key)))
		{
			repeatedQuery.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			(await repeatedQuery.Content.ReadAsStringAsync()).ShouldBeEmpty();
		}

		using var repeatedHeader = new HttpRequestMessage(HttpMethod.Get, "/compat/sonarr/api");
		repeatedHeader.Headers.TryAddWithoutValidation("X-Api-Key", new[] { key, key }).ShouldBeTrue();
		using var repeatedHeaderResponse = await anonymous.SendAsync(repeatedHeader);
		repeatedHeaderResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await repeatedHeaderResponse.Content.ReadAsStringAsync()).ShouldBeEmpty();
	}

	private async Task AssertValidCookieIsNotCompatAuthorizationAsync()
	{
		using var anonymous = _factory.CreateClient();
		var setup = JsonNode.Parse(await anonymous.GetStringAsync("/api/v1/setup/status"))!;
		if (setup["needsSetup"]!.GetValue<bool>())
		{
			using var create = await anonymous.PostAsync(
				"/api/v1/setup",
				JsonContent(JsonNode.Parse("""{"username":"compat-auth","password":"compat-auth-password"}""")!));
			create.StatusCode.ShouldBe(HttpStatusCode.Created);
		}

		using var cookie = _factory.CreateCookieClient();
		using var login = await cookie.PostAsync(
			"/api/v1/auth/login",
			JsonContent(JsonNode.Parse("""{"username":"compat-auth","password":"compat-auth-password","rememberMe":false}""")!));
		login.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var response = await cookie.GetAsync("/compat/sonarr/api");
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
	}

	private static async Task SetAuthAsync(HttpClient admin, JsonNode original, string authMethod, string authenticationRequired)
	{
		var updated = original.DeepClone();
		updated["authMethod"] = authMethod;
		updated["authenticationRequired"] = authenticationRequired;
		using var response = await admin.PutAsync("/api/v1/config/general", JsonContent(updated));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string apiKey)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		request.Headers.Add("X-Api-Key", apiKey);
		return await client.SendAsync(request);
	}

	private static StringContent JsonContent(JsonNode node)
		=> new(node.ToJsonString(), Encoding.UTF8, "application/json");
}
