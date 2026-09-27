using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatCatalogTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatCatalogTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task SonarrAndRadarrCatalogReads_ShouldMatchCompatibilityContractsAndRequireApiKey()
	{
		using var anonymous = _factory.CreateClient();
		(await anonymous.GetAsync("/compat/sonarr/api/v3/qualityprofile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await anonymous.GetAsync("/compat/radarr/api/v3/tag/detail")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		var seeded = await SeedCatalogAsync();
		using var client = await _factory.CreateAuthorizedClientAsync();
		foreach (var (facade, kind) in new[] { ("sonarr", MediaKind.SERIES), ("radarr", MediaKind.MOVIES) })
		{
			var prefix = $"/compat/{facade}/api/v3";
			var profiles = await GetArrayAsync(client, $"{prefix}/qualityprofile");
			profiles.GetArrayLength().ShouldBeGreaterThan(0);
			var profile = profiles.EnumerateArray().First(item => item.GetProperty("id").GetInt32() == seeded.ProfileId);
			profile.GetProperty("name").GetString().ShouldBe("Compat catalog profile");
			profile.GetProperty("items").GetArrayLength().ShouldBe(1);
			profile.GetProperty("items")[0].GetProperty("quality").GetProperty("resolution").ValueKind.ShouldBe(JsonValueKind.Null);
			profile.GetProperty("items")[0].GetProperty("quality").GetProperty("id").GetInt32().ShouldBe(0);
			profile.GetProperty("items")[0].GetProperty("quality").GetProperty("source").GetString().ShouldBe("unknown");
			profile.GetProperty("formatItems").ValueKind.ShouldBe(JsonValueKind.Array);

			var byId = await client.GetAsync($"{prefix}/qualityprofile/{seeded.ProfileId}");
			byId.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await byId.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32().ShouldBe(seeded.ProfileId);
			var schema = await client.GetAsync($"{prefix}/qualityprofile/schema");
			schema.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await schema.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").ValueKind.ShouldBe(JsonValueKind.Array);
			var alias = await GetArrayAsync(client, $"{prefix}/profile");
			alias.GetArrayLength().ShouldBe(profiles.GetArrayLength());
			var languages = await GetArrayAsync(client, $"{prefix}/language");

			var english = languages.EnumerateArray().First(item => item.GetProperty("name").GetString() == "English");
			english.GetProperty("id").GetInt32().ShouldBe(1);
			languages.EnumerateArray().First(item => item.GetProperty("name").GetString() == "Unknown").GetProperty("id").GetInt32().ShouldBe(0);
			languages.EnumerateArray().First(item => item.GetProperty("name").GetString() == "Original").GetProperty("id").GetInt32().ShouldBe(-2);
			var language = await client.GetAsync($"{prefix}/language/1");
			language.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await language.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("English");
			var originalLanguage = await client.GetAsync($"{prefix}/language/-2");
			originalLanguage.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await originalLanguage.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("Original");

			var roots = await GetArrayAsync(client, $"{prefix}/rootfolder");
			roots.GetArrayLength().ShouldBe(1);
			var root = roots[0];
			root.GetProperty("id").GetInt32().ShouldBe(kind == MediaKind.SERIES ? seeded.SeriesRootId : seeded.MovieRootId);
			root.GetProperty("path").GetString().ShouldNotBeNullOrEmpty();
			var freeSpace = root.GetProperty("freeSpace").ValueKind;
			(freeSpace is JsonValueKind.Number or JsonValueKind.Null).ShouldBeTrue();
			root.GetProperty("unmappedFolders").ValueKind.ShouldBe(JsonValueKind.Array);
			var rootDetail = await client.GetAsync($"{prefix}/rootfolder/{root.GetProperty("id").GetInt32()}");
			rootDetail.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await rootDetail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32().ShouldBe(root.GetProperty("id").GetInt32());
			var wrongRootId = kind == MediaKind.SERIES ? seeded.MovieRootId : seeded.SeriesRootId;
			(await client.GetAsync($"{prefix}/rootfolder/{wrongRootId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

			var tags = await GetArrayAsync(client, $"{prefix}/tag");
			tags.GetArrayLength().ShouldBe(1);
			tags[0].GetProperty("label").GetString().ShouldBe("Catalog contract");
			var tagId = tags[0].GetProperty("id").GetInt32();
			var tagDetailResponse = await client.GetAsync($"{prefix}/tag/{tagId}");
			tagDetailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await tagDetailResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("label").GetString().ShouldBe("Catalog contract");
			var details = await GetArrayAsync(client, $"{prefix}/tag/detail");
			details.GetArrayLength().ShouldBe(1);
			var detail = await client.GetAsync($"{prefix}/tag/detail/{tagId}");
			detail.StatusCode.ShouldBe(HttpStatusCode.OK);
			var detailJson = await detail.Content.ReadFromJsonAsync<JsonElement>();
			var ownedIds = detailJson.GetProperty(kind == MediaKind.SERIES ? "seriesIds" : "movieIds");
			ownedIds.GetArrayLength().ShouldBe(1);
			detailJson.GetProperty(kind == MediaKind.SERIES ? "movieIds" : "seriesIds").GetArrayLength().ShouldBe(0);
			detailJson.GetProperty("indexerIds").ValueKind.ShouldBe(JsonValueKind.Array);
			detailJson.GetProperty("notificationIds").ValueKind.ShouldBe(JsonValueKind.Array);
			detailJson.GetProperty("delayProfileIds").ValueKind.ShouldBe(JsonValueKind.Array);
			detailJson.GetProperty("importListIds").ValueKind.ShouldBe(JsonValueKind.Array);
			detailJson.GetProperty("inUse").GetBoolean().ShouldBeTrue();
		}
	}

	private async Task<CatalogIds> SeedCatalogAsync()
	{
		return await _factory.WithDbAsync(async db =>
		{
			var tag = new Tag { Label = "Catalog contract" };
			db.Tags.Add(tag);
			var seriesRoot = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "compat-series-root"), MediaKind = MediaKind.SERIES };
			var movieRoot = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "compat-movie-root"), MediaKind = MediaKind.MOVIES };
			db.RootFolders.AddRange(seriesRoot, movieRoot);
			var quality = QualityResolutionModel.All.First(item => item.Resolution is null);
			var profile = new QualityProfile
			{
				Name = "Compat catalog profile",
				UpgradeAllowed = true,
				Cutoff = 0,
				Items = [new QualityProfileItem(quality, true)]
			};
			db.QualityProfiles.Add(profile);
			var series = new Series { TvdbId = 987654321, Title = "Catalog Series", Tags = [tag] };
			var movie = new Movie { TmdbId = 987654321, Title = "Catalog Movie", Tags = [tag] };
			db.Series.Add(series);
			db.Movies.Add(movie);
			await db.SaveChangesAsync();
			return new CatalogIds(profile.Id, seriesRoot.Id, movieRoot.Id);
		});
	}

	private static async Task<JsonElement> GetArrayAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadFromJsonAsync<JsonElement>();
		json.ValueKind.ShouldBe(JsonValueKind.Array);
		return json;
	}

	private sealed record CatalogIds(int ProfileId, int SeriesRootId, int MovieRootId);
}
