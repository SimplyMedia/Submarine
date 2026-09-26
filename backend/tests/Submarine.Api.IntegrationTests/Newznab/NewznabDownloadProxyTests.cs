using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Api.IntegrationTests.Indexers;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests.Newznab;

/// <summary>
///     The outbound Newznab download proxy must only fetch links through the indexer's own
///     authenticated client when the link points back at that indexer's host (C-F12: SSRF /
///     credential leak otherwise), and must reject malformed input with 400 instead of 500 (C-F23).
/// </summary>
public sealed class NewznabDownloadProxyTests : IAsyncLifetime
{
	private readonly SubmarineApiFactory _factory = new();
	private StubTorznabServer _stub = null!;
	private HttpClient _client = null!;
	private string _apiKey = string.Empty;
	private int _indexerId;

	public async ValueTask InitializeAsync()
	{
		_stub = await StubTorznabServer.StartAsync();
		_client = await _factory.CreateAuthorizedClientAsync();
		_apiKey = await _factory.WithDbAsync(async db => (await db.GeneralConfig.AsNoTracking().SingleAsync()).ApiKey);

		var createBody = new
		{
			name = "Stub",
			implementation = "TORZNAB",
			definitionId = (string?)null,
			protocol = "BITTORRENT",
			baseUrl = _stub.BaseUrl,
			settings = new { baseUrl = _stub.BaseUrl, apiPath = "/api" },
			enableRss = true,
			enableAutomaticSearch = true,
			enableInteractiveSearch = true,
			priority = 25,
			downloadClientId = (int?)null,
			proxyId = (int?)null,
			categories = new[] { 5000 },
			animeCategories = Array.Empty<int>(),
			minimumSeeders = (int?)null,
			seedRatio = (double?)null,
			seedTimeMinutes = (int?)null,
			seasonPackSeedTimeMinutes = (int?)null,
			animeStandardFormatSearch = false,
			tagIds = Array.Empty<int>()
		};
		var created = await _client.PostAsJsonAsync("/api/v1/indexers", createBody);
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var indexer = await created.Content.ReadFromJsonAsync<JsonElement>();
		_indexerId = indexer.GetProperty("id").GetInt32();
	}

	public async ValueTask DisposeAsync()
	{
		_client.Dispose();
		await _stub.DisposeAsync();
		await _factory.DisposeAsync();
	}

	[Fact]
	public async Task DownloadAsync_ShouldReject_WhenLinkHostDoesNotMatchIndexer()
	{
		// evil.invalid (RFC 2606) never resolves; the allowlist check must reject it purely by
		// string comparison against the indexer's configured host, before any connection is attempted.
		var link = EncodeLink("http://evil.invalid/payload");

		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/download?link={link}&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task DownloadAsync_ShouldSucceed_WhenLinkHostMatchesIndexer()
	{
		var link = EncodeLink($"{_stub.BaseUrl}/download/1");

		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/download?link={link}&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).ShouldBe("FAKE_TORRENT_BYTES");
	}

	[Fact]
	public async Task DownloadAsync_ShouldReturn400_WhenLinkIsNotValidBase64()
	{
		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/download?link=%20not-base64!!&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task DownloadAsync_ShouldReject_WhenApiKeyIsWrong()
	{
		var link = EncodeLink($"{_stub.BaseUrl}/download/1");

		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/download?link={link}&apikey=wrong-key");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task NewznabApi_ShouldReturn400_WhenCatIsNotNumeric()
	{
		var response = await _client.GetAsync($"/api/v1/indexers/newznab/api?t=search&cat=not-a-number&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("<error");
	}

	[Fact]
	public async Task OutboundQueryLimit_ShouldReturn429WithRetryAfter()
	{
		await _factory.WithDbAsync(async db =>
		{
			var indexer = await db.Indexers.SingleAsync(entity => entity.Id == _indexerId);
			indexer.QueryLimit = 1;
			indexer.LimitsUnit = IndexerLimitsUnit.HOUR;
			db.IndexerHistories.Add(new IndexerHistory
			{
				IndexerId = _indexerId,
				EventType = IndexerHistoryEventType.QUERY,
				Successful = true,
				Query = "previous request",
				Date = DateTime.UtcNow
			});
			await db.SaveChangesAsync();
			return true;
		});

		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/newznab/api?t=search&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		response.Headers.Contains("Retry-After").ShouldBeTrue();
	}

	[Fact]
	public async Task OutboundGrabLimit_ShouldReturn429WithRetryAfter()
	{
		await _factory.WithDbAsync(async db =>
		{
			var indexer = await db.Indexers.SingleAsync(entity => entity.Id == _indexerId);
			indexer.GrabLimit = 1;
			indexer.LimitsUnit = IndexerLimitsUnit.HOUR;
			db.IndexerHistories.Add(new IndexerHistory
			{
				IndexerId = _indexerId,
				EventType = IndexerHistoryEventType.GRAB,
				Successful = true,
				Query = "previous grab",
				Date = DateTime.UtcNow
			});
			await db.SaveChangesAsync();
			return true;
		});

		var link = EncodeLink($"{_stub.BaseUrl}/download/1");
		var response = await _client.GetAsync($"/api/v1/indexer/{_indexerId}/download?link={link}&apikey={_apiKey}");

		response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		response.Headers.Contains("Retry-After").ShouldBeTrue();
	}

	private static string EncodeLink(string url) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(url));
}
