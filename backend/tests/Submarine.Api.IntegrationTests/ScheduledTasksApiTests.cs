using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class ScheduledTasksApiTests
{
	[Fact]
	public async Task ScheduledTasks_ShouldCreateRunRegisteredCommandAndRejectInvalidOrUnregisteredTasks()
	{
		await using var factory = new SubmarineApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		await factory.WithDbAsync(async db =>
		{
			var existing = await db.ScheduledTasks.SingleAsync(item => item.Name == "CommandCleanup", TestContext.Current.CancellationToken);
			existing.Name = "ExistingCommandCleanup";
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return 0;
		});

		var created = await client.PostAsJsonAsync("/api/v1/scheduled-tasks", new { name = "CommandCleanup", intervalMinutes = 60 });
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var task = await created.Content.ReadFromJsonAsync<JsonElement>();
		var taskId = task.GetProperty("id").GetInt32();

		var run = await client.PostAsync($"/api/v1/scheduled-tasks/{taskId}/run", null);
		run.StatusCode.ShouldBe(HttpStatusCode.Created);
		var command = await run.Content.ReadFromJsonAsync<JsonElement>();
		command.GetProperty("name").GetString().ShouldBe("CommandCleanup");

		var duplicate = await client.PostAsJsonAsync("/api/v1/scheduled-tasks", new { name = "CommandCleanup", intervalMinutes = 30 });
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var invalid = await client.PostAsJsonAsync("/api/v1/scheduled-tasks", new { name = "", intervalMinutes = 0 });
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		await factory.WithDbAsync(async db =>
		{
			var saved = await db.ScheduledTasks.SingleAsync(item => item.Id == taskId, TestContext.Current.CancellationToken);
			saved.Name = "NoSuchRegisteredCommand";
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return 0;
		});
		var unavailable = await client.PostAsync($"/api/v1/scheduled-tasks/{taskId}/run", null);
		unavailable.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
