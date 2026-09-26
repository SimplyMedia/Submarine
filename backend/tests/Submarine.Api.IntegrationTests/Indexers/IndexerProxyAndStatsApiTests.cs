using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests.Indexers;

public sealed class IndexerProxyAndStatsApiTests : IClassFixture<IndexersApiFactory>
{
	private readonly IndexersApiFactory _factory;

	public IndexerProxyAndStatsApiTests(IndexersApiFactory factory) => _factory = factory;

	[Fact]
	public async Task ProxyApi_ShouldCreateUpdateTestListAndDeleteProxyWithoutExposingPassword()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var create = await client.PostAsJsonAsync("/api/v1/indexer-proxies", new
		{
			name = "Local FlareSolverr",
			type = "FLARESOLVERR",
			host = "127.0.0.1",
			port = 1,
			username = "proxy-user",
			password = "secret",
			requestTimeoutSeconds = 1,
			tagIds = Array.Empty<int>()
		});
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var dto = await create.Content.ReadFromJsonAsync<JsonElement>();
		var id = dto.GetProperty("id").GetInt32();
		create.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
		dto.TryGetProperty("password", out _).ShouldBeFalse();

		var listed = await client.GetFromJsonAsync<JsonElement>("/api/v1/indexer-proxies?page=1&pageSize=10");
		listed.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetInt32() == id);

		var update = await client.PutAsJsonAsync($"/api/v1/indexer-proxies/{id}", new
		{
			name = "Updated local proxy",
			type = "FLARESOLVERR",
			host = "127.0.0.1",
			port = 2,
			username = "proxy-user",
			password = "new-secret",
			requestTimeoutSeconds = 2,
			tagIds = Array.Empty<int>()
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("name").GetString().ShouldBe("Updated local proxy");

		var tested = await client.PostAsync($"/api/v1/indexer-proxies/{id}/test", null);
		tested.StatusCode.ShouldBe(HttpStatusCode.OK);
		var testResult = await tested.Content.ReadFromJsonAsync<JsonElement>();
		testResult.GetProperty("isValid").GetBoolean().ShouldBeFalse();

		(await client.DeleteAsync($"/api/v1/indexer-proxies/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync($"/api/v1/indexer-proxies/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task IndexerStats_ShouldAggregateHistoryAndHonorDateAndIndexerFilters()
	{
		var client = await _factory.CreateAuthorizedClientAsync();
		var (indexerId, otherIndexerId, start) = await _factory.WithDbAsync(async db =>
		{
			var indexer = new Indexer { Name = "Stats Primary", BaseUrl = "http://primary.invalid" };
			var other = new Indexer { Name = "Stats Other", BaseUrl = "http://other.invalid" };
			db.Indexers.AddRange(indexer, other);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var from = DateTime.UtcNow.AddMinutes(-1);
			db.IndexerHistories.AddRange(
				new IndexerHistory { IndexerId = indexer.Id, EventType = IndexerHistoryEventType.QUERY, Successful = true, Source = "newznab:client-a", ElapsedMs = 100, Date = from },
				new IndexerHistory { IndexerId = indexer.Id, EventType = IndexerHistoryEventType.GRAB, Successful = true, Source = "newznab:client-a", ElapsedMs = 300, Date = from.AddSeconds(1) },
				new IndexerHistory { IndexerId = indexer.Id, EventType = IndexerHistoryEventType.FAILED, Successful = false, Source = "internal", Date = from.AddSeconds(2) },
				new IndexerHistory { IndexerId = other.Id, EventType = IndexerHistoryEventType.QUERY, Successful = true, Source = "newznab:client-b", ElapsedMs = 40, Date = from });
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (indexer.Id, other.Id, from.AddSeconds(-1));
		});
		var end = DateTime.UtcNow.AddMinutes(1);

		var response = await client.GetFromJsonAsync<JsonElement>($"/api/v1/indexer-stats?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}&indexerIds={indexerId}");
		var stats = response.GetProperty("indexers").EnumerateArray().Single();
		stats.GetProperty("indexerName").GetString().ShouldBe("Stats Primary");
		stats.GetProperty("queryCount").GetInt32().ShouldBe(1);
		stats.GetProperty("grabCount").GetInt32().ShouldBe(1);
		stats.GetProperty("failureCount").GetInt32().ShouldBe(1);
		stats.GetProperty("averageResponseMs").GetDouble().ShouldBe(200);
		var caller = response.GetProperty("userAgents").EnumerateArray().Single();
		caller.GetProperty("userAgent").GetString().ShouldBe("client-a");
		caller.GetProperty("queryCount").GetInt32().ShouldBe(1);
		caller.GetProperty("grabCount").GetInt32().ShouldBe(1);
		otherIndexerId.ShouldNotBe(indexerId);
	}
}
