using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests.Profiles;

/// <summary>
///     Regression coverage for B-F5/C-F27 (UrlBase validation, server-only FeedToken) and
///     B-F18 (config PUT validators, RssSync/DownloadMonitor scheduled task wiring).
/// </summary>
public sealed class ConfigValidationTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public ConfigValidationTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task GeneralConfig_Put_ShouldRejectInvalidUrlBase_AndAcceptValidOne()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/general");

		(await PutJson(client, "/api/v1/config/general", Clone(current, "urlBase", "bad")))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await PutJson(client, "/api/v1/config/general", Clone(current, "urlBase", "/bad/")))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var ok = await PutJson(client, "/api/v1/config/general", Clone(current, "urlBase", "/sub"));
		ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
		(await GetJson(client, "/api/v1/config/general"))!["urlBase"]!.GetValue<string>().ShouldBe("/sub");

		await PutJson(client, "/api/v1/config/general", Clone(current, "urlBase", ""));
	}

	[Fact]
	public async Task GeneralConfig_Put_ShouldIgnoreFeedToken_AndRegenerateEndpointGeneratesA32HexToken()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/general");
		var originalFeedToken = current!["feedToken"]!.GetValue<string>();

		var put = await PutJson(client, "/api/v1/config/general", Clone(current, "feedToken", "attacker-supplied-token"));
		put.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await GetJson(client, "/api/v1/config/general"))!["feedToken"]!.GetValue<string>().ShouldBe(originalFeedToken);

		var regenerate = await client.PostAsync("/api/v1/config/general/feed-token", null);
		regenerate.StatusCode.ShouldBe(HttpStatusCode.OK);
		var regenerated = await regenerate.Content.ReadFromJsonAsync<FeedTokenDto>();
		regenerated!.FeedToken.Length.ShouldBe(32);
		regenerated.FeedToken.ShouldMatch("^[0-9a-fA-F]{32}$");
		regenerated.FeedToken.ShouldNotBe(originalFeedToken);
	}

	[Fact]
	public async Task IndexerConfig_Put_ShouldRejectNegativeValues_AndWireRssSyncInterval()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/indexer");

		(await PutJson(client, "/api/v1/config/indexer", Clone(current, "retentionDays", -1)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var ok = await PutJson(client, "/api/v1/config/indexer", Clone(current, "rssSyncIntervalMinutes", 0));
		ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
		var interval = await _factory.WithDbAsync(async db =>
			(await db.ScheduledTasks.SingleAsync(x => x.Name == "RssSync")).IntervalMinutes);
		interval.ShouldBe(0);

		await PutJson(client, "/api/v1/config/indexer", current!);
	}

	[Fact]
	public async Task DownloadConfig_Put_ShouldRejectNegativeValue_AndWireDownloadMonitorInterval()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/download");

		(await PutJson(client, "/api/v1/config/download", Clone(current, "checkForFinishedDownloadInterval", -5)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var ok = await PutJson(client, "/api/v1/config/download", Clone(current, "checkForFinishedDownloadInterval", 15));
		ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
		var interval = await _factory.WithDbAsync(async db =>
			(await db.ScheduledTasks.SingleAsync(x => x.Name == "DownloadMonitor")).IntervalMinutes);
		interval.ShouldBe(15);

		await PutJson(client, "/api/v1/config/download", current!);
	}

	[Fact]
	public async Task MediaManagementConfig_Put_ShouldValidateChmodAndEnum()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/media-management");

		(await PutJson(client, "/api/v1/config/media-management", Clone(current, "chmodFolder", "999")))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await PutJson(client, "/api/v1/config/media-management", Clone(current, "downloadPropersAndRepacks", 99)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var ok = await PutJson(client, "/api/v1/config/media-management", Clone(current, "chmodFolder", "0755"));
		ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());

		await PutJson(client, "/api/v1/config/media-management", current!);
	}

	[Fact]
	public async Task NamingConfig_Put_ShouldRejectInvalidEnumValues()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/naming");

		(await PutJson(client, "/api/v1/config/naming", Clone(current, "colonReplacement", 99)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await PutJson(client, "/api/v1/config/naming", Clone(current, "multiEpisodeStyle", 99)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task UiConfig_Put_ShouldValidateFirstDayOfWeekAndTheme()
	{
		var client = ApiClient();
		var current = await GetJson(client, "/api/v1/config/ui");

		(await PutJson(client, "/api/v1/config/ui", Clone(current, "firstDayOfWeek", 9)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await PutJson(client, "/api/v1/config/ui", Clone(current, "theme", 99)))
			.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var ok = await PutJson(client, "/api/v1/config/ui", Clone(current, "firstDayOfWeek", 1));
		ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());

		await PutJson(client, "/api/v1/config/ui", current!);
	}

	private static JsonNode Clone<T>(JsonNode? source, string field, T value)
	{
		var clone = source!.DeepClone();
		clone[field] = JsonValue.Create(value);
		return clone;
	}

	private static async Task<JsonNode?> GetJson(HttpClient client, string path)
		=> JsonNode.Parse(await client.GetStringAsync(path));

	private static Task<HttpResponseMessage> PutJson(HttpClient client, string path, JsonNode body)
		=> client.PutAsync(path, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));

	private HttpClient ApiClient()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", _factory.ReadApiKey());
		return client;
	}

	private sealed record FeedTokenDto(string FeedToken);
}
