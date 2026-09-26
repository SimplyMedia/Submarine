using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatProwlarrTests(SubmarineApiFactory factory) : IClassFixture<SubmarineApiFactory>
{
	[Fact]
	public async Task HomepageIndexerAndStatsWorkflow_ReturnsConfiguredIndexerAndRecordedTotals()
	{
		var indexerId = await factory.WithDbAsync(async db =>
		{
			var indexer = new Indexer { Name = "Homepage tracker", Implementation = IndexerImplementation.TORZNAB, Protocol = Protocol.BITTORRENT, BaseUrl = "https://tracker.example/api", Priority = 9, SettingsJson = "{}" };
			db.Indexers.Add(indexer);
			await db.SaveChangesAsync();
			db.IndexerHistories.AddRange(
				new IndexerHistory { IndexerId = indexer.Id, EventType = IndexerHistoryEventType.QUERY, Successful = true, Source = "homepage", ElapsedMs = 120, Date = DateTime.UtcNow },
				new IndexerHistory { IndexerId = indexer.Id, EventType = IndexerHistoryEventType.GRAB, Successful = true, Source = "grab", ElapsedMs = 240, Date = DateTime.UtcNow });
			await db.SaveChangesAsync();
			return indexer.Id;
		});

		using var client = await factory.CreateAuthorizedClientAsync();
		var indexers = await GetAsync(client, "/compat/prowlarr/api/v1/indexer");
		var indexer = indexers.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == indexerId);
		indexer.GetProperty("name").GetString().ShouldBe("Homepage tracker");
		indexer.GetProperty("protocol").GetString().ShouldBe("bittorrent");
		indexer.GetProperty("enable").GetBoolean().ShouldBeTrue();
		(await GetAsync(client, $"/compat/prowlarr/api/v1/indexer/{indexerId}")).GetProperty("id").GetInt32().ShouldBe(indexerId);

		var stats = await GetAsync(client, $"/compat/prowlarr/api/v1/indexerstats?startDate={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}");
		var totals = stats.GetProperty("indexers").EnumerateArray().Single(x => x.GetProperty("indexerId").GetInt32() == indexerId);
		totals.GetProperty("numberOfQueries").GetInt32().ShouldBe(1);
		totals.GetProperty("numberOfGrabs").GetInt32().ShouldBe(1);
		totals.GetProperty("averageResponseTime").GetDouble().ShouldBe(180);
	}

	[Fact]
	public async Task NotifiarrRegistrationWorkflow_PersistsAndUpdatesRealNotification()
	{
		using var client = await factory.CreateAuthorizedClientAsync();
		var firstList = await GetAsync(client, "/compat/prowlarr/api/v1/notification");
		var saved = firstList.EnumerateArray().FirstOrDefault(x => x.GetProperty("name").GetString() == "Notifiarr Connect registration");
		var request = new
		{
			id = saved.ValueKind == JsonValueKind.Object ? saved.GetProperty("id").GetInt32() : (int?)null,
			name = "Notifiarr Connect registration",
			implementation = "Notifiarr",
			configContract = "NotifiarrSettings",
			fields = new[] { new { name = "apiKey", value = "connect-key" } },
			tags = Array.Empty<int>(),
			onGrab = true,
			onHealthIssue = true,
			onHealthRestored = true,
			onApplicationUpdate = true
		};
		var method = saved.ValueKind == JsonValueKind.Object ? HttpMethod.Put : HttpMethod.Post;
		var response = await client.SendAsync(new HttpRequestMessage(method, "/compat/prowlarr/api/v1/notification") { Content = JsonContent.Create(request) });
		response.StatusCode.ShouldBe(saved.ValueKind == JsonValueKind.Object ? HttpStatusCode.OK : HttpStatusCode.Created);
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var created = body.RootElement;
		created.GetProperty("implementation").GetString().ShouldBe("Notifiarr");
		created.GetProperty("fields").EnumerateArray().Single().GetProperty("value").GetString().ShouldBe("connect-key");
		created.GetProperty("onGrab").GetBoolean().ShouldBeTrue();

		var id = created.GetProperty("id").GetInt32();
		var stored = await factory.WithDbAsync(async db => await db.Notifications.FindAsync(id));
		stored.ShouldNotBeNull();
		stored!.Type.ShouldBe(NotificationType.NOTIFIARR);
		stored.SettingsJson.ShouldContain("connect-key");
	}

	private static async Task<JsonElement> GetAsync(HttpClient client, string path)
	{
		using var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		return document.RootElement.Clone();
	}
}
