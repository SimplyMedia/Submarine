using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests.Profiles;

public sealed class MetadataConsumersAndAutoTaggingApiTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public MetadataConsumersAndAutoTaggingApiTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task MetadataConsumer_FullCrud_ShouldRoundTrip()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/metadata-consumers",
			Json("""{ "name": "Kodi", "type": "KODI", "enable": true, "settingsJson": "{\"seriesMetadata\":true}" }"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<MetadataConsumerDto>();
		created!.Type.ShouldBe("KODI");
		created.Enable.ShouldBeTrue();

		var updated = await client.PutAsync($"/api/v1/metadata-consumers/{created.Id}",
			Json("""{ "name": "Kodi", "type": "KODI", "enable": false, "settingsJson": "{}" }"""));
		updated.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedDto = await updated.Content.ReadFromJsonAsync<MetadataConsumerDto>();
		updatedDto!.Enable.ShouldBeFalse();

		var list = await client.GetFromJsonAsync<List<MetadataConsumerDto>>("/api/v1/metadata-consumers");
		list!.ShouldContain(x => x.Id == created.Id);

		(await client.DeleteAsync($"/api/v1/metadata-consumers/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task MetadataConsumer_Schema_ShouldDescribeEveryType()
	{
		var client = ApiClient();

		var schema = await client.GetFromJsonAsync<List<MetadataConsumerSchemaDto>>("/api/v1/metadata-consumers/schema");

		schema!.Select(x => x.Type).ShouldBe(["KODI", "PLEX", "EMBY", "ROKSBOX", "WDTV"], ignoreOrder: true);
	}

	[Fact]
	public async Task MetadataConsumer_Create_ShouldReject_WhenSettingsJsonIsNotAnObject()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/metadata-consumers",
			Json("""{ "name": "Bad", "type": "KODI", "settingsJson": "not-json" }"""));

		create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task AutoTaggingRule_FullCrud_ShouldRoundTrip()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/auto-tagging",
			Json("""
				{
					"name": "Anime tag",
					"enable": true,
					"removeTagsAutomatically": true,
					"specifications": [
						{ "name": "Anime", "type": "SERIES_TYPE", "negate": false, "required": true, "value": "ANIME" }
					],
					"tags": ["anime"]
				}
				"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<AutoTaggingRuleDto>();
		created!.Specifications.ShouldHaveSingleItem();
		created.Tags.ShouldBe(["anime"]);

		var updated = await client.PutAsync($"/api/v1/auto-tagging/{created.Id}",
			Json("""
				{
					"name": "Anime tag renamed",
					"enable": false,
					"removeTagsAutomatically": false,
					"specifications": [],
					"tags": []
				}
				"""));
		updated.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedDto = await updated.Content.ReadFromJsonAsync<AutoTaggingRuleDto>();
		updatedDto!.Enable.ShouldBeFalse();
		updatedDto.Tags.ShouldBeEmpty();

		var list = await client.GetFromJsonAsync<List<AutoTaggingRuleDto>>("/api/v1/auto-tagging");
		list!.ShouldContain(x => x.Id == created.Id);

		(await client.DeleteAsync($"/api/v1/auto-tagging/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task AutoTaggingRule_Create_ShouldReject_WhenNameIsEmpty()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/auto-tagging",
			Json("""{ "name": "", "enable": true, "removeTagsAutomatically": false, "specifications": [], "tags": [] }"""));

		create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private HttpClient ApiClient()
	{
		_factory.CreateClient().Dispose();
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Api-Key", _factory.ReadApiKey());

		return client;
	}

	private static StringContent Json(string payload)
		=> new(payload, Encoding.UTF8, "application/json");

	private sealed record MetadataConsumerDto(int Id, string Name, string Type, bool Enable, string SettingsJson);

	private sealed record MetadataConsumerFieldDescriptorDto(string Name, string Label, string? HelpText, bool Default);

	private sealed record MetadataConsumerSchemaDto(string Type, IReadOnlyList<MetadataConsumerFieldDescriptorDto> Fields);

	private sealed record AutoTaggingSpecificationDto(string Name, string Type, bool Negate, bool Required, object? Value);

	private sealed record AutoTaggingRuleDto(
		int Id,
		string Name,
		bool Enable,
		bool RemoveTagsAutomatically,
		IReadOnlyList<AutoTaggingSpecificationDto> Specifications,
		IReadOnlyList<string> Tags);
}
