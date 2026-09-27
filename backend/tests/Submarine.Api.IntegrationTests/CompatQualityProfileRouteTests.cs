using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatQualityProfileRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatQualityProfileRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SonarrProfileRoutes_ShouldCreateRoundTripGroupedQualityUpdateAndDelete()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		var createdResponse = await client.PostAsJsonAsync("/compat/sonarr/api/v3/qualityprofile", new
		{
			name = "Compat profile",
			upgradeAllowed = true,
			cutoff = 3,
			items = new object[]
			{
				new { quality = new { id = 3, name = "WEB 1080p" }, items = new[] { new { quality = new { id = 3, name = "WEBDL-1080p" }, allowed = true } }, allowed = true },
				new { quality = new { id = 7, name = "Bluray-1080p" }, items = Array.Empty<object>(), allowed = true }
			},
			formatItems = Array.Empty<object>(),
			minFormatScore = 2,
			cutoffFormatScore = 4,
			minUpgradeFormatScore = 1
		});
		createdResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();
		createdResponse.Headers.Location.ShouldNotBeNull();
		created.GetProperty("cutoff").GetInt32().ShouldBe(3);
		created.GetProperty("items")[0].GetProperty("quality").GetProperty("id").GetInt32().ShouldBe(3);
		created.GetProperty("items")[0].GetProperty("quality").GetProperty("name").GetString().ShouldBe("WEBDL-1080p");
		created.GetProperty("items")[0].GetProperty("quality").GetProperty("source").GetString().ShouldBe("web");
		created.GetProperty("items")[1].GetProperty("quality").GetProperty("name").GetString().ShouldBe("Bluray-1080p");
		created.GetProperty("minFormatScore").GetInt32().ShouldBe(2);
		created.GetProperty("cutoffFormatScore").GetInt32().ShouldBe(4);
		created.GetProperty("minUpgradeFormatScore").GetInt32().ShouldBe(1);

		var update = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/qualityprofile/{id}", new
		{
			id,
			name = "Compat profile updated",
			items = created.GetProperty("items"),
			cutoff = created.GetProperty("cutoff").GetInt32(),
			upgradeAllowed = false,
			formatItems = created.GetProperty("formatItems"),
			minFormatScore = created.GetProperty("minFormatScore").GetInt32(),
			cutoffFormatScore = created.GetProperty("cutoffFormatScore").GetInt32(),
			minUpgradeFormatScore = created.GetProperty("minUpgradeFormatScore").GetInt32(),
			createdAt = "ignored-read-only"
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("name").GetString().ShouldBe("Compat profile updated");
		updated.GetProperty("upgradeAllowed").GetBoolean().ShouldBeFalse();
		updated.GetProperty("items").GetArrayLength().ShouldBe(2);
		var batchUpdate = await client.PutAsJsonAsync("/compat/sonarr/api/v3/qualityprofile", new[]
		{
			new
			{
				id,
				name = "Compat profile batch",
				items = updated.GetProperty("items"),
				cutoff = updated.GetProperty("cutoff").GetInt32(),
				upgradeAllowed = false,
				formatItems = updated.GetProperty("formatItems"),
				minFormatScore = updated.GetProperty("minFormatScore").GetInt32(),
				cutoffFormatScore = updated.GetProperty("cutoffFormatScore").GetInt32(),
				minUpgradeFormatScore = updated.GetProperty("minUpgradeFormatScore").GetInt32()
			}
		});
		batchUpdate.StatusCode.ShouldBe(HttpStatusCode.OK);
		var batchProfiles = await batchUpdate.Content.ReadFromJsonAsync<JsonElement>();
		batchProfiles.GetArrayLength().ShouldBe(1);
		batchProfiles[0].GetProperty("name").GetString().ShouldBe("Compat profile batch");


		var delete = await client.DeleteAsync($"/compat/sonarr/api/v3/qualityprofile/{id}");
		delete.StatusCode.ShouldBe(HttpStatusCode.OK);
		var nativeExists = await _factory.WithDbAsync(db => db.QualityProfiles.AnyAsync(profile => profile.Id == id));
		nativeExists.ShouldBeFalse();
	}

	[Fact]
	public async Task ProfileRouteBodyConflictAndUnsupportedGroup_ShouldRejectWithoutMutation()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		var createdResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/qualityprofile", new
		{
			name = "Atomic compat profile",
			upgradeAllowed = true,
			cutoff = 3,
			items = new[] { new { quality = new { id = 3 }, items = Array.Empty<object>(), allowed = true } },
			formatItems = Array.Empty<object>(),
			minFormatScore = 0,
			cutoffFormatScore = 0,
			minUpgradeFormatScore = 0
		});
		createdResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();

		var conflict = await client.PutAsJsonAsync($"/compat/radarr/api/v3/qualityprofile/{id}", new { id = id + 1000, name = "Must not apply" });
		conflict.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var conflictBody = await conflict.Content.ReadFromJsonAsync<JsonElement>();
		conflictBody.ValueKind.ShouldBe(JsonValueKind.Array);

		var unsupported = await client.PutAsJsonAsync($"/compat/radarr/api/v3/qualityprofile/{id}", new
		{
			name = "Must not apply",
			items = new[]
			{
				new
				{
					quality = new { id = 3 },
					items = new[] { new { quality = new { id = 3 }, allowed = true }, new { quality = new { id = 7 }, allowed = true } },
					allowed = true
				}
			},
			cutoff = 3
		});
		unsupported.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var issues = await unsupported.Content.ReadFromJsonAsync<JsonElement>();
		issues.ValueKind.ShouldBe(JsonValueKind.Array);
		issues[0].GetProperty("propertyName").GetString().ShouldBe("items[0].items");

		var unchanged = await _factory.WithDbAsync(db => db.QualityProfiles.AsNoTracking().Where(profile => profile.Id == id).Select(profile => profile.Name).SingleAsync());
		unchanged.ShouldBe("Atomic compat profile");
		(await client.DeleteAsync($"/compat/radarr/api/v3/qualityprofile/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
