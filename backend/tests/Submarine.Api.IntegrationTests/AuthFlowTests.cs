using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Each test gets a fresh factory with its own database, since setup only works once per database.
/// </summary>
public sealed class AuthFlowTests
{
	[Fact]
	public async Task Setup_Status_ShouldReportNeedsSetup_OnFreshDatabase()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateClient();

		var status = await client.GetFromJsonAsync<SetupStatus>("/api/v1/setup/status");

		status!.NeedsSetup.ShouldBeTrue();
	}

	[Fact]
	public async Task Setup_ShouldCreateFirstUser_AndRejectSecond()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateClient();

		var created = await client.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" });
		created.StatusCode.ShouldBe(HttpStatusCode.Created);

		var second = await client.PostAsJsonAsync("/api/v1/setup", new { username = "other", password = "correct-horse" });
		second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Setup_ShouldRejectInvalidPayload()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync("/api/v1/setup", new { username = "", password = "" });

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
		problem!.Errors.ShouldNotBeNull();
		problem.Errors.Keys.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ProtectedEndpoint_ShouldReturn401_WithoutCookie()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateCookieClient();

		var response = await client.GetAsync("/api/v1/commands");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_Me_Logout_Flow_ShouldWork()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" });

		var client = factory.CreateCookieClient();
		var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
		{
			username = "admin",
			password = "correct-horse",
			rememberMe = true
		});
		login.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var me = await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me");
		me!.Username.ShouldBe("admin");

		var logout = await client.PostAsync("/api/v1/auth/logout", null);
		logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task Login_ShouldReturn401_WithWrongPassword()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "badpw", password = "correct-horse" });

		var client = factory.CreateClient();
		var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "badpw", password = "wrong" });

		login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_ShouldReturn401_ForUnknownUsername()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" });

		var client = factory.CreateClient();
		// Regression for C-F21: verifying a password for an unknown user must still run the
		// PBKDF2 hasher (against a dummy hash) instead of short-circuiting, and must not throw.
		var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "does-not-exist", password = "whatever" });

		login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_ShouldBeRateLimited_AfterTenRequestsInFiveMinutes()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateClient();
		// Setup and login share the same "login" rate limit policy/partition (by remote IP).
		(await client.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" }))
			.StatusCode.ShouldBe(HttpStatusCode.Created);

		for (var attempt = 0; attempt < 9; attempt++)
		{
			var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "wrong" });
			response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		}

		var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "wrong" });
		limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		limited.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task Session_ShouldBeRejected_AfterPasswordChangeElsewhere_WithColdCache()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		var created = await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" });
		var user = (await created.Content.ReadFromJsonAsync<UserDto>())!;

		var cookieClient = factory.CreateCookieClient();
		var login = await cookieClient.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "correct-horse" });
		login.StatusCode.ShouldBe(HttpStatusCode.OK);
		// Note: no /me call yet, so the principal cache is still cold for this user.

		var apiClient = await factory.CreateAuthorizedClientAsync();
		var changePassword = await apiClient.PutAsJsonAsync($"/api/v1/users/{user.Id}", new { password = "new-password" });
		changePassword.StatusCode.ShouldBe(HttpStatusCode.OK);

		var me = await cookieClient.GetAsync("/api/v1/auth/me");
		me.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task Session_ShouldBeRejected_WhenUserIsDeleted_WithColdCache()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "admin", password = "correct-horse" });

		var apiClient = await factory.CreateAuthorizedClientAsync();
		var createdSecond = await apiClient.PostAsJsonAsync("/api/v1/users", new { username = "second", password = "correct-horse" });
		var second = (await createdSecond.Content.ReadFromJsonAsync<UserDto>())!;

		var cookieClient = factory.CreateCookieClient();
		var login = await cookieClient.PostAsJsonAsync("/api/v1/auth/login", new { username = "second", password = "correct-horse" });
		login.StatusCode.ShouldBe(HttpStatusCode.OK);
		// Note: no /me call yet, so the principal cache is still cold for this user.

		var delete = await apiClient.DeleteAsync($"/api/v1/users/{second.Id}");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var me = await cookieClient.GetAsync("/api/v1/auth/me");
		me.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task SystemStatus_ShouldReportFormsAuth_AndProvider()
	{
		await using var factory = new SubmarineApiFactory();
		var setupClient = factory.CreateClient();
		await setupClient.PostAsJsonAsync("/api/v1/setup", new { username = "statususer", password = "correct-horse" });

		var client = factory.CreateCookieClient();
		await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "statususer", password = "correct-horse" });

		var status = await client.GetAsync("/api/v1/system/status");
		status.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await status.Content.ReadFromJsonAsync<SystemStatus>();
		body!.AuthMethod.ShouldBe("FORMS");
		body.DatabaseProvider.ShouldBe("Sqlite");
		body.Version.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task HealthEndpoints_ShouldBeAnonymous()
	{
		await using var factory = new SubmarineApiFactory();
		var client = factory.CreateClient();

		(await client.GetAsync("/_status/healthz")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync("/_status/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private sealed record SetupStatus(bool NeedsSetup);

	private sealed record UserDto(int Id, string Username);

	private sealed record SystemStatus(string Version, string DatabaseProvider, string AuthMethod);

	private sealed record ValidationProblem(IDictionary<string, string[]> Errors);
}
