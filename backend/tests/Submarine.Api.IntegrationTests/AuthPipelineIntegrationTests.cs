using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class AuthPipelineIntegrationTests : IClassFixture<RemoteAddressApiFactory>
{
	private readonly RemoteAddressApiFactory _factory;

	public AuthPipelineIntegrationTests(RemoteAddressApiFactory factory) => _factory = factory;

	[Fact]
	public async Task LocalAddressBypass_ShouldTrustOnlyResolvedAddresses()
	{
		using var api = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await api.GetStringAsync("/api/v1/config/general"))!;
		var config = original.DeepClone();
		config["authMethod"] = "FORMS";
		config["authenticationRequired"] = "DISABLED_FOR_LOCAL_ADDRESSES";
		config["trustedProxies"] = "192.168.1.2/32";
		using (var update = await api.PutAsync("/api/v1/config/general", JsonContent(config)))
		{
			update.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		try
		{
			await ExpectStatus("127.0.0.1", HttpStatusCode.OK);
			await ExpectStatus("203.0.113.8", HttpStatusCode.Unauthorized, "127.0.0.1");
			await ExpectStatus("192.168.1.2", HttpStatusCode.Unauthorized, "203.0.113.8");
			await ExpectStatus("192.168.1.2", HttpStatusCode.OK, "192.168.1.40");
		}
		finally
		{
			using var restore = await api.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	[Fact]
	public async Task LoginRateLimit_ShouldPartitionByResolvedClientAddress()
	{
		using var api = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await api.GetStringAsync("/api/v1/config/general"))!;
		var config = original.DeepClone();
		config["trustedProxies"] = "192.168.1.2/32";
		using (var update = await api.PutAsync("/api/v1/config/general", JsonContent(config)))
		{
			update.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		try
		{
			for (var attempt = 0; attempt < 10; attempt++)
			{
				using var response = await Login("192.168.1.2", "203.0.113.8");
				response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			}

			using (var separateClient = await Login("192.168.1.2", "203.0.113.9"))
			{
				separateClient.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			}

			using (var limited = await Login("192.168.1.2", "203.0.113.8"))
			{
				limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
			}
		}
		finally
		{
			using var restore = await api.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	[Fact]
	public async Task BasicAuth_ShouldChallengeAndNegotiateSignalR_AndApiKeysKeepPrecedence()
	{
		await EnsureUserExists();
		using var api = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await api.GetStringAsync("/api/v1/config/general"))!;
		var config = original.DeepClone();
		config["authMethod"] = "BASIC";
		config["authenticationRequired"] = "ENABLED";
		config["trustedProxies"] = "";
		using (var update = await api.PutAsync("/api/v1/config/general", JsonContent(config)))
		{
			update.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		try
		{
			using var client = _factory.CreateClient();
			using (var unauthenticated = await client.GetAsync("/api/v1/system/status"))
			{
				unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
				unauthenticated.Headers.WwwAuthenticate.ShouldContain(challenge => challenge.Scheme == "Basic");
			}

			client.DefaultRequestHeaders.Authorization = Basic("auth-integration", "auth-integration-password");
			using (var good = await client.GetAsync("/api/v1/system/status"))
			{
				good.StatusCode.ShouldBe(HttpStatusCode.OK);
			}

			client.DefaultRequestHeaders.Authorization = Basic("auth-integration", "wrong-password");
			using (var bad = await client.GetAsync("/api/v1/system/status"))
			{
				bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
			}

			using var basicClient = _factory.CreateClient();
			basicClient.DefaultRequestHeaders.Authorization = Basic("auth-integration", "auth-integration-password");
			using var negotiate = await basicClient.PostAsync("/hubs/events/negotiate?negotiateVersion=1", null);
			negotiate.StatusCode.ShouldBe(HttpStatusCode.OK);

			await AssertApiKeyPrecedence(api, "BASIC");
			await AssertApiKeyPrecedence(api, "FORMS");
			await AssertApiKeyPrecedence(api, "NONE");
		}
		finally
		{
			using var restore = await api.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	[Fact]
	public async Task CookieLogin_ShouldNegotiateSignalR()
	{
		await EnsureUserExists();
		using var api = await _factory.CreateAuthorizedClientAsync();
		var original = JsonNode.Parse(await api.GetStringAsync("/api/v1/config/general"))!;
		var config = original.DeepClone();
		config["authMethod"] = "FORMS";
		config["authenticationRequired"] = "ENABLED";
		using (var update = await api.PutAsync("/api/v1/config/general", JsonContent(config)))
		{
			update.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		try
		{
			using var client = _factory.CreateCookieClient();
			using var login = await client.PostAsync("/api/v1/auth/login", JsonContent(JsonNode.Parse("""{"username":"auth-integration","password":"auth-integration-password","rememberMe":false}""")!));
			login.StatusCode.ShouldBe(HttpStatusCode.OK);
			using var negotiate = await client.PostAsync("/hubs/events/negotiate?negotiateVersion=1", null);
			negotiate.StatusCode.ShouldBe(HttpStatusCode.OK);
		}
		finally
		{
			using var restore = await api.PutAsync("/api/v1/config/general", JsonContent(original));
		}
	}

	private async Task EnsureUserExists()
	{
		using var client = _factory.CreateClient();
		var status = JsonNode.Parse(await client.GetStringAsync("/api/v1/setup/status"))!;
		if (status["needsSetup"]!.GetValue<bool>())
		{
			using var setup = await client.PostAsync("/api/v1/setup", JsonContent(JsonNode.Parse("""{"username":"auth-integration","password":"auth-integration-password"}""")!));
			setup.StatusCode.ShouldBe(HttpStatusCode.Created);
		}
	}

	private async Task AssertApiKeyPrecedence(HttpClient api, string authMethod)
	{
		var config = JsonNode.Parse(await api.GetStringAsync("/api/v1/config/general"))!;
		config["authMethod"] = authMethod;
		using (var update = await api.PutAsync("/api/v1/config/general", JsonContent(config)))
		{
			update.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		using var client = _factory.CreateClient();
		var key = _factory.ReadApiKey();
		client.DefaultRequestHeaders.Add("X-Api-Key", key);
		using (var validHeader = await client.GetAsync("/api/v1/system/status"))
		{
			validHeader.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		client.DefaultRequestHeaders.Remove("X-Api-Key");
		client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");
		using (var invalidHeader = await client.GetAsync("/api/v1/system/status"))
		{
			invalidHeader.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		}

		client.DefaultRequestHeaders.Remove("X-Api-Key");
		using (var validQuery = await client.GetAsync($"/api/v1/system/status?apikey={key}"))
		{
			validQuery.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		using (var invalidQuery = await client.GetAsync("/api/v1/system/status?apikey=invalid-key"))
		{
			invalidQuery.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		}
	}

	private async Task ExpectStatus(string peer, HttpStatusCode expected, string? forwardedFor = null)
	{
		using var client = _factory.CreateRemoteClient(peer);
		using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/status");
		if (forwardedFor is not null)
		{
			request.Headers.Add("X-Forwarded-For", forwardedFor);
		}

		using var response = await client.SendAsync(request);
		response.StatusCode.ShouldBe(expected);
	}

	private async Task<HttpResponseMessage> Login(string peer, string forwardedFor)
	{
		using var client = _factory.CreateRemoteClient(peer);
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
		{
			Content = JsonContent(JsonNode.Parse("""{"username":"missing-user","password":"wrong"}""")!)
		};
		request.Headers.Add("X-Forwarded-For", forwardedFor);
		return await client.SendAsync(request);
	}

	private static AuthenticationHeaderValue Basic(string username, string password)
		=> new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

	private static StringContent JsonContent(JsonNode value)
		=> new(value.ToJsonString(), Encoding.UTF8, "application/json");
}

public sealed class RemoteAddressApiFactory : SubmarineApiFactory
{
	public HttpClient CreateRemoteClient(string address)
	{
		CreateClient().Dispose();
		return new HttpClient(Server.CreateHandler(context =>
			context.Connection.RemoteIpAddress = IPAddress.Parse(address)))
		{
			BaseAddress = new Uri("http://localhost")
		};
	}
}
