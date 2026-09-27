using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatCommandRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatCommandRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SonarrAndRadarrCommands_ShouldPersistAndExposeNativeStateThroughPolling()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		foreach (var (facade, name) in new[] { ("sonarr", "RescanSeries"), ("radarr", "RescanMovie"), ("sonarr", "MissingEpisodeSearch") })
		{
			using var response = await client.PostAsync(
				$"/compat/{facade}/api/v3/command",
				new StringContent($$"""{"name":"{{name}}"}""", Encoding.UTF8, "application/json"));

			response.StatusCode.ShouldBe(HttpStatusCode.Created);
			var created = await response.Content.ReadFromJsonAsync<CommandDto>();
			created.ShouldNotBeNull();
			created!.Id.ShouldBeGreaterThan(0);
			created.Name.ShouldBe(name);
			created.Status.ShouldBeOneOf("queued", "started", "completed");
			created.Queued.ShouldNotBe(default);

			var polled = await PollUntilCompletedAsync(client, facade, created.Id);
			polled.Name.ShouldBe(name);
			polled.Status.ShouldBe("completed");
			polled.Progress.ShouldBe(100);
			polled.Body.ValueKind.ShouldBe(JsonValueKind.Object);

			using var listResponse = await client.GetAsync($"/compat/{facade}/api/v3/command");
			listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
			var listed = await listResponse.Content.ReadFromJsonAsync<CommandDto[]>();
			listed.ShouldNotBeNull();
			listed!.ShouldContain(item => item.Id == created.Id && item.Status == "completed");
		}
	}

	[Fact]
	public async Task UnknownCompatCommand_ShouldReturnBadRequest()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		using var response = await client.PostAsync(
			"/compat/radarr/api/v3/command",
			new StringContent("""{"name":"NotACompatCommand"}""", Encoding.UTF8, "application/json"));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	private static async Task<CommandDto> PollUntilCompletedAsync(HttpClient client, string facade, int id)
	{
		for (var attempt = 0; attempt < 50; attempt++)
		{
			using var response = await client.GetAsync($"/compat/{facade}/api/v3/command/{id}");
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
			var command = await response.Content.ReadFromJsonAsync<CommandDto>();
			command.ShouldNotBeNull();
			if (command!.Status is "completed" or "failed" or "aborted")
			{
				return command;
			}

			await Task.Delay(50);
		}

		throw new TimeoutException($"Command {id} did not finish within the polling window.");
	}

	private sealed record CommandDto(
		int Id,
		string Name,
		JsonElement Body,
		string Status,
		DateTime Queued,
		DateTime? Started,
		DateTime? Ended,
		string? Duration,
		string? Message,
		int Progress);
}
