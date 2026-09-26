using Xunit;

using System.Net;
using System.Net.Http.Json;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Models;
using Submarine.Mappings.Tests.TestKit;
using Shouldly;

namespace Submarine.Mappings.Tests;

public sealed class ExportImportIntegrationTests(AdminMappingsTestFactory factory) : IClassFixture<AdminMappingsTestFactory>
{
	private readonly HttpClient _client = factory.CreateAuthorizedClient();

	[Fact]
	public async Task Export_ShouldReturnFullDataset()
	{
		await SeedAsync();

		var response = await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var export = await TestJson.ReadAs<MappingsExport>(response);
		export.ShouldNotBeNull();
		export.SceneMappings.ShouldHaveSingleItem().TvdbId.ShouldBe(400);
		export.EpisodeMappings.ShouldHaveSingleItem().TvdbId.ShouldBe(400);
		export.AniListMappings.ShouldHaveSingleItem().AniListId.ShouldBe(400);
		export.SceneNames.ShouldHaveSingleItem().TvdbId.ShouldBe(400);
	}

	[Fact]
	public async Task Export_ShouldReturnEmptyDataset_WhenNothingExists()
	{
		// Own database: the class fixture is shared with tests that seed rows.
		await using var emptyFactory = new AdminMappingsTestFactory();
		using var client = emptyFactory.CreateAuthorizedClient();
		var response = await client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var export = await TestJson.ReadAs<MappingsExport>(response);
		export.ShouldNotBeNull();
		export.SceneMappings.ShouldBeEmpty();
		export.EpisodeMappings.ShouldBeEmpty();
		export.AniListMappings.ShouldBeEmpty();
		export.SceneNames.ShouldBeEmpty();
	}

	[Fact]
	public async Task Import_ShouldBeIdempotent()
	{
		await SeedAsync();
		var before = await TestJson.ReadNormalizedAsync(await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken));

		var payload = await TestJson.ReadAs<MappingsExport>(await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken));
		payload.ShouldNotBeNull();

		var first = await _client.PostAsJsonAsync("/api/v1/import", payload, TestJson.Options, TestContext.Current.CancellationToken);
		var second = await _client.PostAsJsonAsync("/api/v1/import", payload, TestJson.Options, TestContext.Current.CancellationToken);

		first.StatusCode.ShouldBe(HttpStatusCode.OK);
		second.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadNormalizedAsync(await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken))).ShouldBe(before);
	}

	[Fact]
	public async Task Import_ShouldReproduceDataset_WhenRestoredIntoCleanDatabase()
	{
		await SeedAsync();
		var payload = await TestJson.ReadAs<MappingsExport>(await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken));
		payload.ShouldNotBeNull();

		using var cleanFactory = new MappingsTestFactory(MappingsTestFactory.AdminApiKey);
		var cleanClient = cleanFactory.CreateClient();
		cleanClient.DefaultRequestHeaders.Add("X-Api-Key", MappingsTestFactory.AdminApiKey);

		var response = await cleanClient.PostAsJsonAsync("/api/v1/import", payload, TestJson.Options, TestContext.Current.CancellationToken);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await TestJson.ReadNormalizedAsync(await cleanClient.GetAsync("/api/v1/export", TestContext.Current.CancellationToken)))
			.ShouldBe(await TestJson.ReadNormalizedAsync(await _client.GetAsync("/api/v1/export", TestContext.Current.CancellationToken)));

		// Resolutions must work identically against the restored dataset.
		(await TestJson.ReadAs<SceneResolution>(await cleanClient.GetAsync("/api/v1/scene/400/resolve?season=1&episode=4", TestContext.Current.CancellationToken)))
			.ShouldBe(new SceneResolution(2, 14));
	}

	[Fact]
	public async Task Import_ShouldReturn400_WhenItemIsInvalid()
	{
		var payload = new MappingsExport(
			[new SceneMappingResource(0, 500, "", 1, null, 0, null, null)],
			[],
			[],
			[]);

		var response = await _client.PostAsJsonAsync("/api/v1/import", payload, TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task SeedAsync()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(400, "Season title", 1, 2, 10, null, null), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/scene/episode-mappings",
			new CreateSceneEpisodeMappingRequest(400, 1, 1, 3, 3), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(400, 400, "Cour", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/scene/names",
			new CreateSceneNameRequest(400, "Scene name", null), TestJson.Options, TestContext.Current.CancellationToken);
	}
}
