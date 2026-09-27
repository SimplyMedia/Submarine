using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatQualityRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatQualityRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task QualityDefinitions_ShouldMapExplicitIdsAndApplyNullableBatchAtomically()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		foreach (var facade in new[] { "sonarr", "radarr" })
		{
			var facadeDefinitions = await client.GetFromJsonAsync<JsonElement>($"/compat/{facade}/api/v3/qualitydefinition");
			facadeDefinitions.ValueKind.ShouldBe(JsonValueKind.Array);
			var facadeTarget = facadeDefinitions.EnumerateArray().First(item => item.GetProperty("quality").GetProperty("name").GetString() == "HDTV-720p");
			facadeTarget.GetProperty("quality").GetProperty("id").GetInt32().ShouldBe(facadeTarget.GetProperty("id").GetInt32());
			facadeTarget.GetProperty("title").GetString().ShouldBe("TV-720p");
			facadeTarget.GetProperty("weight").GetInt32().ShouldBe(14);
			facadeTarget.TryGetProperty("minSize", out _).ShouldBeTrue();
			facadeTarget.TryGetProperty("maxSize", out _).ShouldBeTrue();
			facadeTarget.TryGetProperty("preferredSize", out _).ShouldBeTrue();
			var profiles = await client.GetFromJsonAsync<JsonElement>($"/compat/{facade}/api/v3/qualityprofile");
			var hdtv = profiles[0].GetProperty("items").EnumerateArray().First(item => item.GetProperty("quality").GetProperty("name").GetString() == "HDTV-720p");
			hdtv.GetProperty("quality").GetProperty("source").GetString().ShouldBe(facade == "sonarr" ? "television" : "tv");
			hdtv.GetProperty("quality").GetProperty("id").GetInt32().ShouldBe(4);
		}
		var definitions = await client.GetFromJsonAsync<JsonElement>("/compat/sonarr/api/v3/qualitydefinition");
		var target = definitions.EnumerateArray().First(item => item.GetProperty("quality").GetProperty("id").GetInt32() == 4);
		var id = target.GetProperty("id").GetInt32();
		var before = await _factory.WithDbAsync(db => db.QualityDefinitions.Where(item => item.Source == Submarine.Core.Quality.QualitySource.TV && item.Resolution == Submarine.Core.Quality.QualityResolution.R720_P).Select(item => item.MinSizeMbPerMinute).SingleAsync());

		var update = await client.PutAsJsonAsync("/compat/sonarr/api/v3/qualitydefinition/update", new[]
		{
			(object)new { id, minSize = (double?)null, maxSize = 120d, preferredSize = 35d, title = "ignored", weight = 999 },
			new { id = int.MaxValue, minSize = 1d, maxSize = 2d, preferredSize = 1d }
		});
		update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var afterRejectedBatch = await _factory.WithDbAsync(db => db.QualityDefinitions.Where(item => item.Source == Submarine.Core.Quality.QualitySource.TV && item.Resolution == Submarine.Core.Quality.QualityResolution.R720_P).Select(item => item.MinSizeMbPerMinute).SingleAsync());
		afterRejectedBatch.ShouldBe(before);

		update = await client.PutAsJsonAsync("/compat/sonarr/api/v3/qualitydefinition/update", new[]
		{
			(object)new { id, minSize = (double?)null, maxSize = 120d, preferredSize = 35d, title = "ignored", weight = 999 }
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);

		var changed = await _factory.WithDbAsync(db => db.QualityDefinitions.Where(item => item.Source == Submarine.Core.Quality.QualitySource.TV && item.Resolution == Submarine.Core.Quality.QualityResolution.R720_P).SingleAsync());
		changed.MinSizeMbPerMinute.ShouldBeNull();
		changed.MaxSizeMbPerMinute.ShouldBe(120d);
		changed.PreferredSizeMbPerMinute.ShouldBe(35d);
		changed.Title.ShouldBe(target.GetProperty("title").GetString());
	}

	[Fact]
	public async Task CustomFormats_ShouldExposeSchemaRoundTripSpecsAndRequireAuth()
	{
		using var anonymous = _factory.CreateClient();
		(await anonymous.GetAsync("/compat/radarr/api/v3/customformat/schema")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var client = await _factory.CreateAuthorizedClientAsync();
		var schema = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/customformat/schema");
		schema.ValueKind.ShouldBe(JsonValueKind.Array);
		schema.EnumerateArray().ShouldContain(item => item.GetProperty("implementation").GetString() == "ProtocolSpecification" && item.GetProperty("fields").ValueKind == JsonValueKind.Array);

		var create = await client.PostAsJsonAsync("/compat/radarr/api/v3/customformat", new
		{
			name = "compat numeric protocol",
			specifications = new object[]
			{
				new { name = "Protocol", implementation = "ProtocolSpecification", negate = false, required = true, fields = new object[] { new { name = "value", value = 0 } } },
				new { name = "Language", implementation = "LanguageSpecification", negate = false, required = true, fields = new object[] { new { name = "value", value = 1 } } },
				new { name = "Source", implementation = "SourceSpecification", negate = false, required = true, fields = new object[] { new { name = "value", value = 6 } } },
				new { name = "Resolution", implementation = "ResolutionSpecification", negate = false, required = true, fields = new object[] { new { name = "value", value = 1080 } } },
				new { name = "Title", implementation = "ReleaseTitleSpecification", negate = false, required = false, fields = new object[] { new { name = "value", value = "(?i)remux" } } }
			}
		});
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();
		var stored = await _factory.WithDbAsync(db => db.CustomFormats.Where(item => item.Id == id).Select(item => item.Specifications).SingleAsync());
		stored[0].Value!.Value.GetString().ShouldBe("BITTORRENT");
		stored[1].Value!.Value.GetString().ShouldBe("ENGLISH");
		stored[2].Value!.Value.GetString().ShouldBe("WEB_DL");
		stored[3].Value!.Value.GetString().ShouldBe("R1080_P");

		var get = await client.GetFromJsonAsync<JsonElement>($"/compat/radarr/api/v3/customformat/{id}");
		get.GetProperty("specifications")[0].GetProperty("fields")[0].GetProperty("value").GetInt32().ShouldBe(0);
		var update = await client.PutAsJsonAsync($"/compat/radarr/api/v3/customformat/{id}", get);
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("id").GetInt32().ShouldBe(id);
		updated.GetProperty("specifications").GetArrayLength().ShouldBe(5);

		var delete = await client.DeleteAsync($"/compat/radarr/api/v3/customformat/{id}");
		delete.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync($"/compat/radarr/api/v3/customformat/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CustomFormatIdUpdate_ShouldRejectBodyRouteMismatch()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		var create = await client.PostAsJsonAsync("/compat/sonarr/api/v3/customformat", new { name = "id mismatch format", specifications = Array.Empty<object>() });
		create.StatusCode.ShouldBe(HttpStatusCode.Created);
		var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
		var mismatch = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/customformat/{id}", new { id = id + 1, name = "changed" });
		mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
