using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests.Profiles;

public sealed class CustomFormatsApiTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CustomFormatsApiTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task CustomFormat_FullCrud_ShouldRoundTrip()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/custom-formats",
			Json("""
				{
					"name": "Crud HD",
					"includeCustomFormatWhenRenaming": true,
					"specifications": [
						{ "name": "1080p", "type": "RESOLUTION", "negate": false, "required": true, "value": "R1080_P" }
					]
				}
				"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<CustomFormatDto>();
		created!.Specifications.ShouldHaveSingleItem();

		var duplicate = await client.PostAsync("/api/v1/custom-formats",
			Json("""{ "name": "Crud HD", "includeCustomFormatWhenRenaming": false, "specifications": [] }"""));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var updated = await client.PutAsync($"/api/v1/custom-formats/{created!.Id}",
			Json("""
				{
					"name": "Crud HD Renamed",
					"includeCustomFormatWhenRenaming": false,
					"specifications": []
				}
				"""));
		updated.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await client.DeleteAsync($"/api/v1/custom-formats/{created.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task CustomFormat_Import_ShouldAcceptTrashObjectAndArray()
	{
		var client = ApiClient();

		var single = await client.PostAsync("/api/v1/custom-formats/import",
			Json("""
				{
					"name": "BR-DISK",
					"includeCustomFormatWhenRenaming": false,
					"specifications": [
						{ "name": "BR-DISK", "implementation": "ReleaseTitleSpecification", "negate": false, "required": true, "fields": { "value": "\\bBD\\b" } }
					]
				}
				"""));
		single.StatusCode.ShouldBe(HttpStatusCode.Created);
		(await single.Content.ReadFromJsonAsync<List<CustomFormatDto>>())!
			.ShouldHaveSingleItem().Name.ShouldBe("BR-DISK");

		var array = await client.PostAsync("/api/v1/custom-formats/import",
			Json("""
				[
					{ "name": "Imported One", "includeCustomFormatWhenRenaming": false, "specifications": [] },
					{ "name": "Imported Two", "includeCustomFormatWhenRenaming": false, "specifications": [] }
				]
				"""));
		array.StatusCode.ShouldBe(HttpStatusCode.Created);
		(await array.Content.ReadFromJsonAsync<List<CustomFormatDto>>())!.Count.ShouldBe(2);

		var unknown = await client.PostAsync("/api/v1/custom-formats/import",
			Json("""
				{
					"name": "Mystery",
					"includeCustomFormatWhenRenaming": false,
					"specifications": [
						{ "name": "Mystery", "implementation": "MysterySpecification", "negate": false, "required": true, "fields": { "value": "x" } }
					]
				}
				"""));
		unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task CustomFormat_Export_ShouldProduceTrashJson()
	{
		var client = ApiClient();
		var created = await client.PostAsync("/api/v1/custom-formats",
			Json("""
				{
					"name": "Export Me",
					"includeCustomFormatWhenRenaming": false,
					"specifications": [
						{ "name": "Size", "type": "SIZE", "negate": false, "required": true, "value": { "min": 2, "max": 10 } }
					]
				}
				"""));
		var format = await created.Content.ReadFromJsonAsync<CustomFormatDto>();

		var export = await client.GetAsync($"/api/v1/custom-formats/{format!.Id}/export");
		export.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var json = await export.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
		json!.RootElement.GetProperty("name").GetString().ShouldBe("Export Me");
		json.RootElement.GetProperty("specifications")[0].GetProperty("implementation").GetString()
			.ShouldBe("SizeSpecification");
		json.RootElement.GetProperty("specifications")[0].GetProperty("fields").GetProperty("min")
			.GetDouble().ShouldBe(2);
	}

	[Fact]
	public async Task CustomFormat_Test_ShouldReportMatchingFormats()
	{
		var client = ApiClient();
		await client.PostAsync("/api/v1/custom-formats",
			Json("""
				{
					"name": "HD Test",
					"includeCustomFormatWhenRenaming": false,
					"specifications": [
						{ "name": "1080p", "type": "RESOLUTION", "negate": false, "required": true, "value": "R1080_P" }
					]
				}
				"""));

		var test = await client.PostAsync("/api/v1/custom-formats/test",
			Json("""{ "title": "Series.Title.S01E01.1080p.WEB-DL-GROUP", "size": 2000000000 }"""));
		test.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await test.Content.ReadFromJsonAsync<TestResultDto>();
		result!.Matched.ShouldContain(format => format.Name == "HD Test");

		var unparsable = await client.PostAsync("/api/v1/custom-formats/test",
			Json("""{ "title": "totally not a release" }"""));
		unparsable.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task QualityDefinition_List_And_BulkUpdate_ShouldWork()
	{
		var client = ApiClient();

		var list = await client.GetFromJsonAsync<List<QualityDefinitionDto>>("/api/v1/quality-definitions");
		list!.ShouldNotBeEmpty();

		var first = list.First();
		var update = await client.PutAsync("/api/v1/quality-definitions",
			Json($"[ {{ \"id\": {first.Id}, \"minSizeMbPerMinute\": 1.5, \"maxSizeMbPerMinute\": 20, \"preferredSizeMbPerMinute\": 8 }} ]"));
		update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var changed = await client.PutAsync($"/api/v1/quality-definitions/{first.Id}",
			Json($$"""{ "id": {{first.Id}}, "minSizeMbPerMinute": 2, "maxSizeMbPerMinute": 1 }"""));
		changed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var reloaded = await client.GetFromJsonAsync<List<QualityDefinitionDto>>("/api/v1/quality-definitions");
		reloaded!.First(definition => definition.Id == first.Id).MinSizeMbPerMinute.ShouldBe(1.5);
	}

	[Fact]
	public async Task ReleaseFilter_FullCrud_ShouldWork()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/release-filters",
			Json("""{ "field": "RELEASE_GROUP", "values": ["EVO"], "mode": "BLOCK", "tier": 0 }"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<ReleaseFilterDto>();
		created!.Field.ShouldBe("RELEASE_GROUP");
		created.Mode.ShouldBe("BLOCK");

		(await client.DeleteAsync($"/api/v1/release-filters/{created!.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task QualityOverride_FullCrud_ShouldWork_AndConflictOnDuplicateGroup()
	{
		var client = ApiClient();

		var create = await client.PostAsync("/api/v1/quality-overrides",
			Json("""{ "releaseGroup": "TestGroup", "source": "BLURAY" }"""));
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<QualityOverrideDto>();
		created!.Source.ShouldBe("BLURAY");

		var duplicate = await client.PostAsync("/api/v1/quality-overrides",
			Json("""{ "releaseGroup": "TestGroup", "source": "WEB_DL" }"""));
		duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		var update = await client.PutAsync($"/api/v1/quality-overrides/{created!.Id}",
			Json("""{ "releaseGroup": "TestGroup", "source": "WEB_DL" }"""));
		update.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await client.DeleteAsync($"/api/v1/quality-overrides/{created.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.NoContent);
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

	private sealed record CustomFormatDto(
		int Id,
		string Name,
		bool IncludeCustomFormatWhenRenaming,
		IReadOnlyList<SpecificationDto> Specifications);

	private sealed record SpecificationDto(
		string Name,
		string Type,
		bool Negate,
		bool Required,
		object? Value);

	private sealed record TestResultDto(IReadOnlyList<CustomFormatDto> Matched, int Score);

	private sealed record QualityDefinitionDto(
		int Id,
		string? Source,
		string? Resolution,
		string Title,
		double? MinSizeMbPerMinute,
		double? MaxSizeMbPerMinute,
		double? PreferredSizeMbPerMinute);

	private sealed record ReleaseFilterDto(int Id, string Field, IReadOnlyList<string> Values, string Mode, int Tier);

	private sealed record QualityOverrideDto(int Id, string ReleaseGroup, string Source);
}
