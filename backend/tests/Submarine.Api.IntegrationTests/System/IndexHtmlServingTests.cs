using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Shouldly;
using Xunit;
namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Regression coverage for C-F28/FE-F2: the SPA fallback rewrites asset and baseURL references
///     for the configured UrlBase, while API routes under a different base still 404 instead of
///     falling back to the index page.
/// </summary>
public sealed class IndexHtmlServingTests
{
	private static readonly string FixtureWebRoot = Path.Combine(AppContext.BaseDirectory, "System", "Fixtures", "wwwroot");

	[Fact]
	public async Task UrlBase_ShouldRewriteIndexHtml_AndServeApiUnderIt_AndNotFallBackForUnknownApiRoutes()
	{
		await using var factory = new UrlBaseTestFactory { WebRoot = FixtureWebRoot };

		// Set UrlBase through the real endpoint so it invalidates the auth-config snapshot cache,
		// same as an admin would; the API key works before setup completes.
		var apiClient = await factory.CreateAuthorizedClientAsync();
		var current = await apiClient.GetFromJsonAsync<JsonNode>("/api/v1/config/general");
		current!["urlBase"] = "/sub";
		(await apiClient.PutAsJsonAsync("/api/v1/config/general", current)).StatusCode.ShouldBe(HttpStatusCode.OK);

		var client = factory.CreateClient();

		var index = await client.GetAsync("/sub/");
		index.StatusCode.ShouldBe(HttpStatusCode.OK);
		var html = await index.Content.ReadAsStringAsync();
		html.ShouldContain("/sub/_nuxt/entry.js");
		html.ShouldContain("baseURL:\"/sub\"");

		var status = await client.GetAsync("/sub/api/v1/setup/status");
		status.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await status.Content.ReadFromJsonAsync<SetupStatusDto>();
		body!.NeedsSetup.ShouldBeTrue();

		var unknown = await client.GetAsync("/api/v1/unknown");
		unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		unknown.Content.Headers.ContentType?.MediaType.ShouldNotBe("text/html");
	}


	private sealed record SetupStatusDto(bool NeedsSetup);

	private sealed class UrlBaseTestFactory : SubmarineApiFactory
	{
		public required string WebRoot { get; init; }

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			builder.UseWebRoot(WebRoot);
		}
	}
}
