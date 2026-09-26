using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests.Profiles;

public sealed class ProfilesApiTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public ProfilesApiTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task QualityProfile_FullCrud_ShouldRoundTrip()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/quality-profiles",
			Json("""
				{
					"name": "Crud HD",
					"upgradeAllowed": true,
					"cutoff": 1,
					"items": [
						{ "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": true },
						{ "quality": { "source": "WEB_DL", "resolution": "R1080_P", "name": "WebDL-1080p" }, "allowed": true }
					],
					"formatItems": [],
					"minFormatScore": 0,
					"cutoffFormatScore": 0,
					"minUpgradeFormatScore": 0
				}
				"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);

		var created = await create.Content.ReadFromJsonAsync<QualityProfileDto>();
		created!.Name.ShouldBe("Crud HD");
		created.Items.Count.ShouldBe(2);

		var update = await client.PutAsync($"/api/v1/quality-profiles/{created.Id}",
			Json("""
				{
					"name": "Crud HD Renamed",
					"upgradeAllowed": false,
					"cutoff": 0,
					"items": [
						{ "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": true }
					],
					"formatItems": [],
					"minFormatScore": 0,
					"cutoffFormatScore": 0,
					"minUpgradeFormatScore": 0
				}
				"""));
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await update.Content.ReadFromJsonAsync<QualityProfileDto>();
		updated!.Name.ShouldBe("Crud HD Renamed");
		updated.UpgradeAllowed.ShouldBeFalse();

		var list = await client.GetAsync("/api/v1/quality-profiles");
		list.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await client.DeleteAsync($"/api/v1/quality-profiles/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.GetAsync($"/api/v1/quality-profiles/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task QualityProfile_Update_ShouldAcceptSourceOnlyQualities_WhenSavingTheSeededProfile()
	{
		var client = ApiClient();
		var seeded = await client.GetFromJsonAsync<JsonElement>("/api/v1/quality-profiles?PageSize=1", TestContext.Current.CancellationToken);
		var any = seeded.GetProperty("items")[0];
		var id = any.GetProperty("id").GetInt32();
		var body = any.GetRawText().Replace("\"name\":\"Any\"", "\"name\":\"Any (edited)\"");

		var update = await client.PutAsync($"/api/v1/quality-profiles/{id}", Json(body), TestContext.Current.CancellationToken);

		update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
		updated.GetProperty("name").GetString().ShouldBe("Any (edited)");
		updated.GetProperty("items").GetArrayLength().ShouldBe(any.GetProperty("items").GetArrayLength());
	}

	[Fact]
	public async Task QualityProfile_Create_ShouldConflict_WhenNameExists()
	{
		var client = ApiClient();
		await CreateProfile(client, "Duplicate HD");

		var duplicate = await client.PostAsync("/api/v1/quality-profiles",
			Json($$"""
				{
					"name": "Duplicate HD",
					"upgradeAllowed": true,
					"cutoff": 0,
					"items": [ { "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": true } ],
					"formatItems": []
				}
				"""));

		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task QualityProfile_Create_ShouldReturn400_WhenNoQualityIsAllowed()
	{
		var client = ApiClient();

		var response = await client.PostAsync("/api/v1/quality-profiles",
			Json("""
				{
					"name": "Nothing Allowed",
					"upgradeAllowed": true,
					"cutoff": 0,
					"items": [ { "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": false } ],
					"formatItems": []
				}
				"""));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task QualityProfile_Create_ShouldReturn400_WhenCutoffIsDisallowed()
	{
		var client = ApiClient();

		var response = await client.PostAsync("/api/v1/quality-profiles",
			Json("""
				{
					"name": "Bad Cutoff",
					"upgradeAllowed": true,
					"cutoff": 1,
					"items": [
						{ "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": true },
						{ "quality": { "source": "WEB_DL", "resolution": "R1080_P", "name": "WebDL-1080p" }, "allowed": false }
					],
					"formatItems": []
				}
				"""));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task QualityProfile_Template_ShouldCreateProfile_AndConflictOnDuplicateName()
	{
		var client = ApiClient();

		var created = await client.PostAsync("/api/v1/quality-profiles/from-template/HD-1080p", null);
		created.StatusCode.ShouldBe(HttpStatusCode.Created);

		var profile = await created.Content.ReadFromJsonAsync<QualityProfileDto>();
		profile!.Name.ShouldBe("HD-1080p");
		profile.Items.Count.ShouldBeGreaterThan(1);
		profile.Items.Count(item => item.Allowed).ShouldBeGreaterThan(1);

		(await client.PostAsync("/api/v1/quality-profiles/from-template/HD-1080p", null))
			.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.PostAsync("/api/v1/quality-profiles/from-template/Nope", null))
			.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task QualityProfile_Clone_ShouldCopyItems_WithUniqueName()
	{
		var client = ApiClient();
		var sourceId = await CreateProfile(client, "Clone Source");

		var first = await client.PostAsync($"/api/v1/quality-profiles/{sourceId}/clone", null);
		first.StatusCode.ShouldBe(HttpStatusCode.Created);
		(await first.Content.ReadFromJsonAsync<QualityProfileDto>())!.Name.ShouldBe("Clone Source Copy");

		var second = await client.PostAsync($"/api/v1/quality-profiles/{sourceId}/clone", null);
		second.StatusCode.ShouldBe(HttpStatusCode.Created);
		(await second.Content.ReadFromJsonAsync<QualityProfileDto>())!.Name.ShouldBe("Clone Source Copy 2");
	}

	[Fact]
	public async Task QualityProfile_Schema_ShouldGroupQualitiesByResolution()
	{
		var client = ApiClient();

		var response = await client.GetAsync("/api/v1/quality-profiles/schema");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var schema = await response.Content.ReadFromJsonAsync<List<SchemaGroupDto>>();
		var group = schema!.Single(entry => entry.Resolution == "R1080_P");
		group.Qualities.ShouldContain(quality => quality.Source == "WEB_DL" && quality.Name == "WebDL-1080p");
	}

	[Fact]
	public async Task QualityProfile_Delete_ShouldConflict_WhenProfileIsInUse()
	{
		var client = ApiClient();
		var profileId = await CreateProfile(client, "In Use Profile");

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			var rootFolder = new Core.Entities.RootFolder { Path = $"/tmp/in-use-{profileId}", MediaKind = Core.Enums.MediaKind.SERIES };
			db.RootFolders.Add(rootFolder);
			await db.SaveChangesAsync();

			db.Series.Add(new Core.Entities.Series
			{
				TvdbId = 990_000 + profileId,
				Title = "In Use",
				CleanTitle = "in use",
				Versions =
				[
					new Core.Entities.MediaVersion
					{
						Name = "1080p",
						QualityProfileId = profileId,
						LanguageProfileId = 1,
						RootFolderId = rootFolder.Id,
						Path = "In Use"
					}
				]
			});
			await db.SaveChangesAsync();
		}

		var delete = await client.DeleteAsync($"/api/v1/quality-profiles/{profileId}");

		delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await delete.Content.ReadAsStringAsync()).ShouldContain("In Use");
	}

	[Fact]
	public async Task LanguageProfile_FullCrud_ShouldRoundTrip()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/language-profiles",
			Json("""{ "name": "German", "languages": ["GERMAN", "ENGLISH"], "cutoff": "ENGLISH", "upgradeAllowed": true }"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<LanguageProfileDto>();
		created!.Languages.ShouldBe(["GERMAN", "ENGLISH"]);

		var duplicate = await client.PostAsync("/api/v1/language-profiles",
			Json("""{ "name": "German", "languages": ["GERMAN"], "cutoff": "GERMAN", "upgradeAllowed": true }"""));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var invalid = await client.PostAsync("/api/v1/language-profiles",
			Json("""{ "name": "Bad Cutoff", "languages": ["GERMAN"], "cutoff": "ENGLISH", "upgradeAllowed": true }"""));
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var schema = await client.GetAsync("/api/v1/language-profiles/schema");
		schema.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await client.DeleteAsync($"/api/v1/language-profiles/{created!.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task DelayProfile_FullCrud_AndReorder_ShouldWork()
	{
		var client = ApiClient();

		var first = await client.PostAsync("/api/v1/delay-profiles",
			Json("""{ "name": "Delay One", "preferredProtocol": "USENET", "usenetDelayMinutes": 0, "torrentDelayMinutes": 60, "bypassIfHighestQuality": false, "bypassIfAboveCustomFormatScore": false, "minimumCustomFormatScore": 0, "tags": [] }"""));
		var second = await client.PostAsync("/api/v1/delay-profiles",
			Json("""{ "name": "Delay Two", "preferredProtocol": "BITTORRENT", "usenetDelayMinutes": 0, "torrentDelayMinutes": 30, "bypassIfHighestQuality": true, "bypassIfAboveCustomFormatScore": false, "minimumCustomFormatScore": 0, "tags": [] }"""));

		var firstDto = await first.Content.ReadFromJsonAsync<DelayProfileDto>();
		var secondDto = await second.Content.ReadFromJsonAsync<DelayProfileDto>();

		var reorder = await client.PutAsync("/api/v1/delay-profiles/reorder",
			Json($"[{secondDto!.Id}, {firstDto!.Id}]"));
		reorder.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var list = await client.GetFromJsonAsync<List<DelayProfileDto>>("/api/v1/delay-profiles");
		list!.First().Id.ShouldBe(secondDto.Id);

		(await client.PutAsync("/api/v1/delay-profiles/reorder", Json("[999999]")))
			.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		(await client.DeleteAsync($"/api/v1/delay-profiles/{firstDto.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await client.DeleteAsync($"/api/v1/delay-profiles/{secondDto.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task ReleaseProfile_FullCrud_ShouldWork()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/release-profiles",
			Json("""{ "name": "Anime Terms", "enabled": true, "required": ["WEB-DL"], "ignored": ["/\\bCAM\\b/"] }"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<ReleaseProfileDto>();
		created!.Required.ShouldBe(["WEB-DL"]);

		var badRegex = await client.PostAsync("/api/v1/release-profiles",
			Json("""{ "name": "Bad Regex", "enabled": true, "required": ["/[unclosed/"], "ignored": [] }"""));
		badRegex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var unknownIndexer = await client.PostAsync("/api/v1/release-profiles",
			Json("""{ "name": "Unknown Indexer", "enabled": true, "required": [], "ignored": [], "indexerId": 424242 }"""));
		unknownIndexer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		(await client.DeleteAsync($"/api/v1/release-profiles/{created!.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	private static async Task<int> CreateProfile(HttpClient client, string name)
	{
		var response = await client.PostAsync("/api/v1/quality-profiles",
			Json($$"""
				{
					"name": "{{name}}",
					"upgradeAllowed": true,
					"cutoff": 0,
					"items": [ { "quality": { "source": "WEB_DL", "resolution": "R720_P", "name": "WebDL-720p" }, "allowed": true } ],
					"formatItems": []
				}
				"""));
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return (await response.Content.ReadFromJsonAsync<QualityProfileDto>())!.Id;
	}

	private HttpClient ApiClient()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", ReadApiKey());

		return client;
	}

	private string ReadApiKey()
	{
		return _factory.ReadApiKey();
	}

	private static StringContent Json(string payload)
		=> new(payload, Encoding.UTF8, "application/json");

	private sealed record QualityProfileDto(
		int Id,
		string Name,
		bool UpgradeAllowed,
		int Cutoff,
		IReadOnlyList<ItemDto> Items,
		IReadOnlyList<FormatItemDto> FormatItems,
		int MinFormatScore,
		int CutoffFormatScore,
		int MinUpgradeFormatScore);

	private sealed record ItemDto(QualityDto Quality, bool Allowed);

	private sealed record QualityDto(string? Source, string? Resolution, string Name);

	private sealed record FormatItemDto(int CustomFormatId, int Score);

	private sealed record LanguageProfileDto(int Id, string Name, IReadOnlyList<string> Languages, string Cutoff, bool UpgradeAllowed);

	private sealed record DelayProfileDto(
		int Id,
		string Name,
		string PreferredProtocol,
		int UsenetDelayMinutes,
		int TorrentDelayMinutes,
		bool BypassIfHighestQuality,
		bool BypassIfAboveCustomFormatScore,
		int MinimumCustomFormatScore,
		int Order,
		IReadOnlyList<int> Tags);

	private sealed record ReleaseProfileDto(
		int Id,
		string Name,
		bool Enabled,
		IReadOnlyList<string> Required,
		IReadOnlyList<string> Ignored,
		int? IndexerId,
		IReadOnlyList<int> Tags);

	private sealed record SchemaGroupDto(string Resolution, IReadOnlyList<QualityDto> Qualities);
}
