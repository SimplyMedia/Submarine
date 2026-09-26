using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Xunit;

namespace Submarine.Api.IntegrationTests.Indexers;

public sealed class IndexersEndpointsTests : IAsyncLifetime
{
	private readonly IndexersApiFactory _factory = new();
	private StubTorznabServer _stub = null!;
	private HttpClient _client = null!;

	public async ValueTask InitializeAsync()
	{
		_stub = await StubTorznabServer.StartAsync();
		_client = await _factory.CreateAuthorizedClientAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_client.Dispose();
		await _stub.DisposeAsync();
		await _factory.DisposeAsync();
	}

	[Fact]
	public async Task Definitions_ShouldListBundledCardigannDefinitions()
	{
		var response = await _client.GetAsync("/api/v1/indexer-definitions");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var definitions = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
		definitions!.Count.ShouldBeGreaterThan(0);
		definitions.ShouldAllBe(definition => definition.GetProperty("installed").GetBoolean() == false);
	}

	[Fact]
	public async Task Newznab_ShouldRejectBadApiKey_ButAcceptCorrectKey()
	{
		var rejected = await _client.GetAsync("/api/v1/indexers/newznab/api?t=caps&apikey=not-the-key");
		rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await rejected.Content.ReadAsStringAsync()).ShouldContain("code=\"100\"");

		var apiKey = await _factory.WithDbAsync(async db => (await db.GeneralConfig.AsNoTracking().SingleAsync()).ApiKey);
		var accepted = await _client.GetAsync($"/api/v1/indexers/newznab/api?t=caps&apikey={apiKey}");
		accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await accepted.Content.ReadAsStringAsync()).ShouldContain("<caps>");
	}

	[Fact]
	public async Task IndexerLifecycle_ShouldCreateTestSearchGrabAndExposeViaOutboundNewznab()
	{
		// create
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
		var indexerId = indexer.GetProperty("id").GetInt32();

		// test
		var testResponse = await _client.PostAsync($"/api/v1/indexers/{indexerId}/test", null);
		testResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var testResult = await testResponse.Content.ReadFromJsonAsync<JsonElement>();
		testResult.GetProperty("isValid").GetBoolean().ShouldBeTrue();
		testResult.GetProperty("capabilities").GetProperty("searchAvailable").GetBoolean().ShouldBeTrue();

		// capabilities
		var capabilities = await (await _client.GetAsync($"/api/v1/indexers/{indexerId}/capabilities")).Content.ReadFromJsonAsync<JsonElement>();
		capabilities.GetProperty("tvSearchAvailable").GetBoolean().ShouldBeTrue();

		// seed a minimal library item to match against
		var (seriesId, mediaVersionId) = await SeedLibraryAsync();

		// interactive search
		var searchResponse = await _client.GetAsync($"/api/v1/search?seriesId={seriesId}");
		searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var results = await searchResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
		results!.Count.ShouldBe(1);
		var release = results[0];
		release.GetProperty("guid").GetString().ShouldBe("stub-guid-1");
		var episodeIds = release.GetProperty("episodeIds").EnumerateArray().Select(item => item.GetInt32()).ToArray();
		episodeIds.ShouldNotBeEmpty();
		var decisions = release.GetProperty("decisions").EnumerateArray().ToList();
		decisions.ShouldContain(decision => decision.GetProperty("mediaVersionId").GetInt32() == mediaVersionId && decision.GetProperty("approved").GetBoolean());

		// grab
		var grabBody = new
		{
			guid = release.GetProperty("guid").GetString(),
			indexerId,
			mediaVersionId,
			seriesId = (int?)seriesId,
			episodeIds,
			movieId = (int?)null,
			qualitySource = (string?)null,
			qualityResolution = (string?)null,
			languages = (List<string>?)null
		};
		var grabResponse = await _client.PostAsJsonAsync("/api/v1/releases/grab", grabBody);
		grabResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await grabResponse.Content.ReadAsStringAsync());
		var grabResult = await grabResponse.Content.ReadFromJsonAsync<JsonElement>();
		grabResult.GetProperty("grabbed").GetBoolean().ShouldBeTrue();

		await _factory.WithDbAsync(async db =>
		{
			(await db.TrackedDownloads.CountAsync()).ShouldBe(1);
			(await db.HistoryEvents.CountAsync(history => history.Type == HistoryEventType.GRABBED)).ShouldBe(1);
			(await db.IndexerHistories.CountAsync(history => history.EventType == IndexerHistoryEventType.GRAB)).ShouldBe(1);
			return true;
		});

		// outbound newznab caps + search with the real api key
		var apiKey = await _factory.WithDbAsync(async db => (await db.GeneralConfig.AsNoTracking().SingleAsync()).ApiKey);

		var capsXml = await (await _client.GetAsync($"/api/v1/indexers/newznab/api?t=caps&apikey={apiKey}")).Content.ReadAsStringAsync();
		capsXml.ShouldContain("<caps>");
		capsXml.ShouldContain("id=\"5000\"");

		var searchXml = await (await _client.GetAsync($"/api/v1/indexers/newznab/api?t=search&apikey={apiKey}")).Content.ReadAsStringAsync();
		searchXml.ShouldContain("<item>");
		searchXml.ShouldContain("stub-guid-1");
		searchXml.ShouldContain($"apikey={apiKey}");
	}

	private async Task<(int SeriesId, int MediaVersionId)> SeedLibraryAsync()
		=> await _factory.WithDbAsync(async db =>
		{
			var qualityProfile = await db.QualityProfiles.AsNoTracking().FirstAsync();
			var languageProfile = await db.LanguageProfiles.AsNoTracking().FirstOrDefaultAsync();
			if (languageProfile is null)
			{
				languageProfile = new LanguageProfile { Name = "Any", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
				db.LanguageProfiles.Add(languageProfile);
				await db.SaveChangesAsync();
			}

			var rootFolder = new RootFolder { Path = $"/tmp/submarine-it-root-{Guid.NewGuid():N}", MediaKind = MediaKind.SERIES };
			db.RootFolders.Add(rootFolder);

			var series = new Series { TvdbId = 1, Title = "Some Show", CleanTitle = "someshow", Type = SeriesType.STANDARD };
			db.Series.Add(series);
			await db.SaveChangesAsync();

			db.Episodes.Add(new Episode
			{
				SeriesId = series.Id,
				SeasonNumber = 1,
				EpisodeNumber = 1,
				AirDateUtc = DateTime.UtcNow.AddDays(-1),
				Monitored = true
			});

			var version = new MediaVersion
			{
				SeriesId = series.Id,
				Name = "1080p",
				Path = "Some Show",
				QualityProfileId = qualityProfile.Id,
				LanguageProfileId = languageProfile.Id,
				RootFolderId = rootFolder.Id
			};
			db.MediaVersions.Add(version);

			var torrentFolder = Path.Combine(Path.GetTempPath(), $"it-torrents-{Guid.NewGuid():N}");
			var watchFolder = Path.Combine(Path.GetTempPath(), $"it-watch-{Guid.NewGuid():N}");
			Directory.CreateDirectory(torrentFolder);
			Directory.CreateDirectory(watchFolder);

			db.DownloadClients.Add(new DownloadClient
			{
				Name = "Blackhole",
				Type = DownloadClientType.TORRENT_BLACKHOLE,
				Enable = true,
				SettingsJson = JsonSerializer.Serialize(new { torrentFolder, watchFolder })
			});

			await db.SaveChangesAsync();
			return (series.Id, version.Id);
		});
}
