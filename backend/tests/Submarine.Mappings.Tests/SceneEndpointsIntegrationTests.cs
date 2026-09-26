using Xunit;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Models;
using Submarine.Mappings.Tests.TestKit;
using Shouldly;

namespace Submarine.Mappings.Tests;

public sealed class SceneEndpointsIntegrationTests(AdminMappingsTestFactory factory) : IClassFixture<AdminMappingsTestFactory>
{
	private readonly HttpClient _client = factory.CreateAuthorizedClient();

	[Fact]
	public async Task GetScene_ShouldReturnEmptySet_WhenNoMappingsExist()
	{
		var response = await _client.GetAsync("/api/v1/scene/99", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var set = await TestJson.ReadAs<SceneMappingSetResource>(response);
		set.ShouldNotBeNull();
		set.TvdbId.ShouldBe(99);
		set.Mappings.ShouldBeEmpty();
		set.EpisodeMappings.ShouldBeEmpty();
	}

	[Fact]
	public async Task CreateSceneMapping_ShouldReturnCreated()
	{
		var request = new CreateSceneMappingRequest(TvdbId: 100, Title: "Community title", SeasonNumber: 1, SceneSeasonNumber: 2, EpisodeOffset: 10, SearchTitle: null, Comment: null);

		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings", request, TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		response.Headers.Location.ShouldNotBeNull().ToString().ShouldContain("/api/v1/scene/mappings/");
		var created = await TestJson.ReadAs<SceneMappingResource>(response);
		created.ShouldNotBeNull();
		created.Id.ShouldBeGreaterThan(0);
		created.TvdbId.ShouldBe(100);
		created.Title.ShouldBe("Community title");
		created.SeasonNumber.ShouldBe(1);
		created.SceneSeasonNumber.ShouldBe(2);
		created.EpisodeOffset.ShouldBe(10);
	}

	[Fact]
	public async Task GetScene_ShouldReturnMappingAfterCreate()
	{
		var request = new CreateSceneMappingRequest(TvdbId: 101, Title: "Title", SeasonNumber: null, SceneSeasonNumber: null, EpisodeOffset: 24, SearchTitle: "search", Comment: "comment");
		var created = await TestJson.ReadAs<SceneMappingResource>(await _client.PostAsJsonAsync("/api/v1/scene/mappings", request, TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var response = await _client.GetAsync("/api/v1/scene/101", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var set = await TestJson.ReadAs<SceneMappingSetResource>(response);
		set.ShouldNotBeNull();
		var mapping = set.Mappings.ShouldHaveSingleItem();
		mapping.Id.ShouldBe(created.Id);
		mapping.SeasonNumber.ShouldBeNull();
		mapping.EpisodeOffset.ShouldBe(24);
		mapping.SearchTitle.ShouldBe("search");
		mapping.Comment.ShouldBe("comment");
	}

	[Fact]
	public async Task UpdateSceneMapping_ShouldApplyChanges()
	{
		var created = await TestJson.ReadAs<SceneMappingResource>(await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(102, "Old", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var response = await _client.PutAsJsonAsync($"/api/v1/scene/mappings/{created.Id}",
			new UpdateSceneMappingRequest(102, "New", 1, 3, 5, null, "changed"), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await TestJson.ReadAs<SceneMappingResource>(response);
		updated.ShouldNotBeNull();
		updated.Id.ShouldBe(created.Id);
		updated.Title.ShouldBe("New");
		updated.SceneSeasonNumber.ShouldBe(3);
		updated.EpisodeOffset.ShouldBe(5);
		updated.Comment.ShouldBe("changed");
	}

	[Fact]
	public async Task UpdateSceneMapping_ShouldReturn404_WhenIdIsUnknown()
	{
		var response = await _client.PutAsJsonAsync("/api/v1/scene/mappings/9999",
			new UpdateSceneMappingRequest(102, "New", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task DeleteSceneMapping_ShouldRemoveMapping()
	{
		var created = await TestJson.ReadAs<SceneMappingResource>(await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(103, "Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var delete = await _client.DeleteAsync($"/api/v1/scene/mappings/{created.Id}");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var set = await TestJson.ReadAs<SceneMappingSetResource>(await _client.GetAsync("/api/v1/scene/103", TestContext.Current.CancellationToken));
		set.ShouldNotBeNull();
		set.Mappings.ShouldBeEmpty();
	}

	[Fact]
	public async Task DeleteSceneMapping_ShouldReturn404_WhenIdIsUnknown()
	{
		var response = await _client.DeleteAsync("/api/v1/scene/mappings/9999", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Resolve_ShouldApplySeasonMapping()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(110, "Title", 1, 2, 10, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/scene/110/resolve?season=1&episode=4", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var resolution = await TestJson.ReadAs<SceneResolution>(response);
		resolution.ShouldBe(new SceneResolution(2, 14));
	}

	[Fact]
	public async Task Resolve_ShouldPassthrough_WhenUnmapped()
	{
		var response = await _client.GetAsync("/api/v1/scene/111/resolve?season=1&episode=4", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadAs<SceneResolution>(response)).ShouldBe(new SceneResolution(1, 4));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReverseSeasonMapping()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(112, "Title", 1, 2, 10, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/scene/112/resolve-tvdb?sceneSeason=2&sceneEpisode=14", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadAs<TvdbResolution>(response)).ShouldBe(new TvdbResolution(112, 1, 4, 0));
	}

	[Fact]
	public async Task Resolve_ShouldPreferEpisodeOverride_OverSeasonMapping()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(113, "Title", 1, 2, 10, null, null), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/scene/episode-mappings",
			new CreateSceneEpisodeMappingRequest(113, 1, 1, 5, 5), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/scene/113/resolve?season=1&episode=1", TestContext.Current.CancellationToken);

		(await TestJson.ReadAs<SceneResolution>(response)).ShouldBe(new SceneResolution(5, 5));
	}

	[Fact]
	public async Task Search_ShouldFindSeriesBySceneTitle_CaseInsensitive()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/names",
			new CreateSceneNameRequest(55, "Some Scene Title", null), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(56, "Another Scene Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/scene/search?name=some%20scene", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var ids = await TestJson.ReadAs<List<int>>(response);
		ids.ShouldNotBeNull();
		ids.ShouldContain(55);
	}

	[Fact]
	public async Task Search_ShouldPreferExactMatch_OverPrefixMatch()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/names",
			new CreateSceneNameRequest(60, "Naruto", null), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/scene/names",
			new CreateSceneNameRequest(61, "Naruto Shippuden", null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/scene/search?name=naruto", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var ids = await TestJson.ReadAs<List<int>>(response);
		ids.ShouldNotBeNull();
		ids.ShouldBe([60]);
	}

	[Fact]
	public async Task Names_ShouldCreateAndList()
	{
		var created = await TestJson.ReadAs<SceneNameResource>(await _client.PostAsJsonAsync("/api/v1/scene/names",
			new CreateSceneNameRequest(57, "Alt title", 2), TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var response = await _client.GetAsync("/api/v1/scene/names/57", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var names = await TestJson.ReadAs<List<SceneNameResource>>(response);
		names.ShouldNotBeNull();
		names.ShouldContain(n => n.Id == created.Id && n.SceneName == "Alt title" && n.SeasonNumber == 2);
	}

	[Fact]
	public async Task CreateSceneMapping_ShouldConflict_WhenDuplicateWildcardExists()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(120, "First", null, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(120, "Second", null, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task CreateSceneMapping_ShouldConflict_WhenDuplicateSeasonExists()
	{
		await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(121, "First", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(121, "Second", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task CreateSceneMapping_ShouldAllowExactSeason_AlongsideWildcard()
	{
		var wildcard = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(122, "Wildcard", null, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);
		var exact = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(122, "Exact", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		wildcard.StatusCode.ShouldBe(HttpStatusCode.Created);
		exact.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task CreateSceneMapping_ShouldReturn400_WhenTitleIsMissing()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(123, "", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await TestJson.ReadAs<JsonElement>(response);
		problem.TryGetProperty("errors", out _).ShouldBeTrue();
	}
}

/// <summary>
/// Write auth checks against an instance with no admin key configured.
/// </summary>
public sealed class UnconfiguredAuthIntegrationTests : IDisposable
{
	private readonly MappingsTestFactory _factory = new();
	private readonly HttpClient _client;

	public UnconfiguredAuthIntegrationTests()
	{
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Write_ShouldReturn503_WhenNoAdminKeyIsConfigured()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(130, "Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
	}

	[Fact]
	public async Task Import_ShouldReturn503_WhenNoAdminKeyIsConfigured()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/import",
			new MappingsExport([], [], [], []), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
	}

	[Fact]
	public async Task Read_ShouldStayPublic_WhenNoAdminKeyIsConfigured()
	{
		var response = await _client.GetAsync("/api/v1/scene/1", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	public void Dispose()
	{
		_client.Dispose();
		_factory.Dispose();
	}
}

/// <summary>
/// Write auth checks against an instance with an admin key configured.
/// </summary>
public sealed class ConfiguredAuthIntegrationTests : IDisposable
{
	private readonly MappingsTestFactory _factory = new(MappingsTestFactory.AdminApiKey);
	private readonly HttpClient _client;

	public ConfiguredAuthIntegrationTests()
	{
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Write_ShouldReturn401_WhenApiKeyIsMissing()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(131, "Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Import_ShouldReturn401_WhenApiKeyIsMissing()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/import",
			new MappingsExport([], [], [], []), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Write_ShouldReturn401_WhenApiKeyIsWrong()
	{
		_client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(131, "Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Write_ShouldCreate_WhenApiKeyIsCorrect()
	{
		_client.DefaultRequestHeaders.Add("X-Api-Key", MappingsTestFactory.AdminApiKey);

		var response = await _client.PostAsJsonAsync("/api/v1/scene/mappings",
			new CreateSceneMappingRequest(132, "Title", 1, null, 0, null, null), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Read_ShouldStayPublic_WhenApiKeyIsConfigured()
	{
		var response = await _client.GetAsync("/api/v1/scene/1", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	public void Dispose()
	{
		_client.Dispose();
		_factory.Dispose();
	}
}
