using Xunit;

using System.Net;
using System.Net.Http.Json;
using Submarine.Contracts.Mappings;
using Submarine.Mappings.Tests.TestKit;
using Shouldly;

namespace Submarine.Mappings.Tests;

public sealed class AniListEndpointsIntegrationTests(AdminMappingsTestFactory factory) : IClassFixture<AdminMappingsTestFactory>
{
	private readonly HttpClient _client = factory.CreateAuthorizedClient();

	[Fact]
	public async Task GetAniList_ShouldReturn404_WhenUnmapped()
	{
		var response = await _client.GetAsync("/api/v1/anilist/999", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task CreateAniList_ShouldReturnCreated()
	{
		var request = new CreateAniListMappingRequest(AniListId: 21, TvdbId: 300, Title: "Cour 1", TvdbSeason: 1, EpisodeStart: 1, EpisodeCount: 12, AbsoluteOffset: 0);

		var response = await _client.PostAsJsonAsync("/api/v1/anilist", request, TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		response.Headers.Location.ShouldNotBeNull().ToString().ShouldContain("/api/v1/anilist/21");
		var created = await TestJson.ReadAs<AniListMappingResource>(response);
		created.ShouldNotBeNull();
		created.AniListId.ShouldBe(21);
		created.EpisodeCount.ShouldBe(12);
	}

	[Fact]
	public async Task CreateAniList_ShouldConflict_WhenAniListIdExists()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(22, 300, "First", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(22, 301, "Second", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task GetAniList_ShouldReturnMapping()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(23, 310, "Arc", 1, 1, null, 0), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/anilist/23", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var mapping = await TestJson.ReadAs<AniListMappingResource>(response);
		mapping.ShouldNotBeNull();
		mapping.TvdbId.ShouldBe(310);
		mapping.EpisodeCount.ShouldBeNull();
	}

	[Fact]
	public async Task ResolveAniList_ShouldReturnTvdbResolution()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(24, 320, "Cour 2", 1, 13, 12, 12), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/anilist/24/resolve?episode=1", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadAs<TvdbResolution>(response)).ShouldBe(new TvdbResolution(320, 1, 13, 13));
	}

	[Fact]
	public async Task ResolveAniList_ShouldReturn404_WhenEpisodeIsOutOfRange()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(25, 330, "Cour", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		var beyond = await _client.GetAsync("/api/v1/anilist/25/resolve?episode=13", TestContext.Current.CancellationToken);
		var before = await _client.GetAsync("/api/v1/anilist/25/resolve?episode=0", TestContext.Current.CancellationToken);

		beyond.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		before.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GetTvdbAniList_ShouldReturnOrderedMappings()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(26, 340, "S1 second", 1, 13, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(27, 340, "S1 first", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(28, 340, "S2", 2, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/tvdb/340/anilist", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var mappings = await TestJson.ReadAs<List<AniListMappingResource>>(response);
		mappings.ShouldNotBeNull();
		mappings.Select(m => m.AniListId).ShouldBe([27, 26, 28]);
	}

	[Fact]
	public async Task GetTvdbAniList_ShouldReturnEmpty_WhenUnmapped()
	{
		var response = await _client.GetAsync("/api/v1/tvdb/341/anilist", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadAs<List<AniListMappingResource>>(response)).ShouldBeEmpty();
	}

	[Fact]
	public async Task ResolveTvdbToAniList_ShouldReturnRelativeEpisode()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(29, 350, "Cour 1", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(30, 350, "Cour 2", 1, 13, 12, 12), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/tvdb/350/anilist/resolve?season=1&episode=14", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await TestJson.ReadAs<AniListResolution>(response)).ShouldBe(new AniListResolution(30, 2));
	}

	[Fact]
	public async Task ResolveTvdbToAniList_ShouldReturn404_WhenNoEntryCovers()
	{
		await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(31, 360, "Cour 1", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		var response = await _client.GetAsync("/api/v1/tvdb/360/anilist/resolve?season=1&episode=13", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task UpdateAniList_ShouldApplyChanges()
	{
		var created = await TestJson.ReadAs<AniListMappingResource>(await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(32, 370, "Old", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var response = await _client.PutAsJsonAsync($"/api/v1/anilist/{created.Id}",
			new UpdateAniListMappingRequest(32, 370, "New", 1, 1, null, 5), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await TestJson.ReadAs<AniListMappingResource>(response);
		updated.ShouldNotBeNull();
		updated.Title.ShouldBe("New");
		updated.EpisodeCount.ShouldBeNull();
		updated.AbsoluteOffset.ShouldBe(5);
	}

	[Fact]
	public async Task UpdateAniList_ShouldReturn404_WhenIdIsUnknown()
	{
		var response = await _client.PutAsJsonAsync("/api/v1/anilist/9999",
			new UpdateAniListMappingRequest(32, 370, "New", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DeleteAniList_ShouldRemoveMapping()
	{
		var created = await TestJson.ReadAs<AniListMappingResource>(await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(33, 380, "Arc", 1, 1, 12, 0), TestJson.Options, TestContext.Current.CancellationToken));
		created.ShouldNotBeNull();

		var deleted = await _client.DeleteAsync($"/api/v1/anilist/{created.Id}");

		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await _client.GetAsync("/api/v1/anilist/33", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DeleteAniList_ShouldReturn404_WhenIdIsUnknown()
	{
		var response = await _client.DeleteAsync("/api/v1/anilist/9999", TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CreateAniList_ShouldReturn400_WhenEpisodeStartIsZero()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(34, 390, "Arc", 1, 0, 12, 0), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task CreateAniList_ShouldReturn400_WhenEpisodeCountIsZero()
	{
		var response = await _client.PostAsJsonAsync("/api/v1/anilist",
			new CreateAniListMappingRequest(35, 391, "Arc", 1, 1, 0, 0), TestJson.Options, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
