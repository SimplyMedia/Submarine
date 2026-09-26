using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Submarine.Core.Commands;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class ApiKeyAndCommandsTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public ApiKeyAndCommandsTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task ApiKey_Header_ShouldAuthenticate()
	{
		_factory.CreateClient().Dispose();
		var apiKey = ReadApiKey();

		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

		(await client.GetAsync("/api/v1/commands")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task ApiKey_QueryParameter_ShouldAuthenticate()
	{
		_factory.CreateClient().Dispose();
		var apiKey = ReadApiKey();

		var client = _factory.CreateClient();
		(await client.GetAsync($"/api/v1/commands?apikey={apiKey}")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task ApiKey_ShouldBeRejected_WhenWrong()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", "not-the-real-key");

		(await client.GetAsync("/api/v1/commands")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Command_Enqueue_ShouldShowUp_AndComplete()
	{
		var apiKey = ReadApiKey();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

		var created = await client.PostAsync(
			"/api/v1/commands",
			new StringContent("""{"name":"CommandCleanup"}""", Encoding.UTF8, "application/json"));
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var row = await created.Content.ReadFromJsonAsync<CommandRow>();
		// The executor with parallelism 2 may already have claimed the row.
		row!.Status.ShouldBeOneOf("QUEUED", "RUNNING");
		row.Name.ShouldBe("CommandCleanup");

		var final = await PollUntilCompletedAsync(client, row.Id);
		final.Status.ShouldBe("COMPLETED");
		final.Progress.ShouldBe(100);

		// Dedupe: enqueueing the identical command while nothing is queued creates a new row.
		var second = await client.PostAsync(
			"/api/v1/commands",
			new StringContent("""{"name":"CommandCleanup"}""", Encoding.UTF8, "application/json"));
		second.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Command_UnknownName_ShouldReturn400()
	{
		_factory.CreateClient().Dispose();
		var apiKey = ReadApiKey();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

		var response = await client.PostAsync(
			"/api/v1/commands",
			new StringContent("""{"name":"NoSuchCommand"}""", Encoding.UTF8, "application/json"));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ScheduledTasks_ShouldBeSeeded_AndListable()
	{
		_factory.CreateClient().Dispose();
		var apiKey = ReadApiKey();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

		var response = await client.GetAsync("/api/v1/scheduled-tasks?page=1&pageSize=50");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<Page>();
		page!.TotalCount.ShouldBeGreaterThanOrEqualTo(9);
		page.Items.ShouldContain(x => x.Name == "CommandCleanup");
	}

	private string ReadApiKey()
	{
		return _factory.ReadApiKey();
	}

	private static async Task<CommandRow> PollUntilCompletedAsync(HttpClient client, int id)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			var response = await client.GetAsync($"/api/v1/commands/{id}");
			var row = await response.Content.ReadFromJsonAsync<CommandRow>();
			if (row!.Status is "COMPLETED" or "FAILED" or "CANCELLED")
			{
				return row;
			}

			await Task.Delay(200);
		}

		throw new TimeoutException($"Command {id} did not finish in time");
	}

	private sealed record CommandRow(int Id, string Name, string Status, int Progress);

	private sealed record Page(IReadOnlyList<NameRow> Items, int TotalCount);

	private sealed record NameRow(string Name);
}
