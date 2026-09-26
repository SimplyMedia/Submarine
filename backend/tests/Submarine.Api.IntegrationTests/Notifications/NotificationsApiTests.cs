using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Submarine.Api.Features.Notifications;
using Xunit;

namespace Submarine.Api.IntegrationTests.Notifications;

public sealed class NotificationsApiTests : IClassFixture<NotificationsApiFactory>
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly NotificationsApiFactory _factory;

	public NotificationsApiTests(NotificationsApiFactory factory) => _factory = factory;

	[Fact]
	public async Task Schema_ShouldDescribeEveryType_WithSupportedEvents()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var response = await client.GetAsync("/api/v1/notifications/schema");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var schemas = (await response.Content.ReadFromJsonAsync<List<NotificationSchemaDto>>(JsonOptions))!;

		var join = schemas.Single(s => s.Type == "JOIN");
		join.Fields.ShouldContain(f => f.Name == "apiKey" && f.Required);
		join.SupportedEvents.ShouldContain("GRAB");
		join.SupportedEvents.ShouldNotContain("RENAME");

		var plex = schemas.Single(s => s.Type == "PLEX");
		plex.SupportedEvents.ShouldNotContain("GRAB");
		plex.SupportedEvents.ShouldContain("RENAME");
	}

	[Fact]
	public async Task TestUnsaved_ShouldSucceed_ForNewProviderAgainstStub()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var response = await client.PostAsJsonAsync("/api/v1/notifications/test", new
		{
			type = "SENDGRID",
			settingsJson = """{"apiKey":"sg","from":"a@b.c","recipients":["d@e.f"]}"""
		});

		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task TestUnsaved_ShouldReturnBadRequest_WhenSettingsInvalid()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var response = await client.PostAsJsonAsync("/api/v1/notifications/test", new
		{
			type = "MAILGUN",
			settingsJson = """{"apiKey":"mg"}"""
		});

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Notification_FullCrudPlusSavedTest_ShouldRoundTrip_ForNewProvider()
	{
		var client = await _factory.CreateAuthorizedClientAsync();

		var create = await client.PostAsJsonAsync("/api/v1/notifications", new
		{
			name = "My Join",
			type = "JOIN",
			enable = true,
			settingsJson = """{"apiKey":"jk","priority":0}""",
			onGrab = true,
			onImport = true
		});
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<NotificationDto>(JsonOptions);
		created!.Name.ShouldBe("My Join");

		var test = await client.PostAsync($"/api/v1/notifications/{created.Id}/test", null);
		test.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		(await client.DeleteAsync($"/api/v1/notifications/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync("/api/v1/notifications")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
