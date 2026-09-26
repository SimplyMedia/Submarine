using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Enums;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatConfigRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatConfigRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task NamingAndMediaManagementRoutes_ShouldUpdateNativeFieldsAndPreserveSiblingSettings()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		await _factory.WithDbAsync(async db =>
		{
			var naming = await db.NamingConfig.FindAsync(1);
			naming!.DailyEpisodeFormat = "native-daily-format";
			var media = await db.MediaManagementConfig.FindAsync(1);
			media!.WriteNfo = true;
			media.EnableMediaInfo = false;
			await db.SaveChangesAsync();
			return true;
		});

		using var sonarrNamingGet = await client.GetAsync("/compat/sonarr/api/v3/config/naming");
		sonarrNamingGet.StatusCode.ShouldBe(HttpStatusCode.OK);
		var sonarrNaming = await sonarrNamingGet.Content.ReadFromJsonAsync<JsonElement>();
		sonarrNaming.GetProperty("id").GetInt32().ShouldBe(1);
		sonarrNaming.TryGetProperty("standardEpisodeFormat", out _).ShouldBeTrue();
		sonarrNaming.TryGetProperty("standardMovieFormat", out _).ShouldBeFalse();

		using var radarrNamingGet = await client.GetAsync("/compat/radarr/api/v3/config/naming");
		var radarrNaming = await radarrNamingGet.Content.ReadFromJsonAsync<JsonElement>();
		radarrNaming.GetProperty("standardMovieFormat").GetString().ShouldNotBeNullOrWhiteSpace();
		using var namingPut = await client.PutAsJsonAsync("/compat/radarr/api/v3/config/naming/1", new
		{
			id = 1,
			standardMovieFormat = "{Movie Title} - compat",
			colonReplacement = radarrNaming.GetProperty("colonReplacement").GetInt32(),
			multiEpisodeStyle = radarrNaming.GetProperty("multiEpisodeStyle").GetInt32()
		});
		namingPut.StatusCode.ShouldBe(HttpStatusCode.OK);
		await _factory.WithDbAsync(async db =>
		{
			var config = await db.NamingConfig.FindAsync(1);
			config!.MovieFormat.ShouldBe("{Movie Title} - compat");
			config.DailyEpisodeFormat.ShouldBe("native-daily-format");
			return true;
		});

		using var mediaPut = await client.PutAsJsonAsync("/compat/sonarr/api/v3/config/mediamanagement", new
		{
			id = 1,
			useHardlinks = false,
			minimumFreeSpaceWhenImporting = 321,
			recycleBin = "/compat-recycle",
			autoUnmonitorPreviouslyDownloadedEpisodes = true,
			downloadPropersAndRepacks = 0
		});
		mediaPut.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updatedMedia = await mediaPut.Content.ReadFromJsonAsync<JsonElement>();
		updatedMedia.GetProperty("id").GetInt32().ShouldBe(1);
		updatedMedia.GetProperty("downloadPropersAndRepacks").GetInt32().ShouldBe(0);
		await _factory.WithDbAsync(async db =>
		{
			var config = await db.MediaManagementConfig.FindAsync(1);
			config!.UseHardlinks.ShouldBeFalse();
			config.MinimumFreeSpaceMb.ShouldBe(321);
			config.RecycleBinPath.ShouldBe("/compat-recycle");
			config.UnmonitorDeletedFiles.ShouldBeTrue();
			config.DownloadPropersAndRepacks.ShouldBe(DownloadPropersAndRepacks.DO_NOT_UPGRADE);
			config.WriteNfo.ShouldBeTrue();
			config.EnableMediaInfo.ShouldBeFalse();
			return true;
		});

		using var radarrMediaGet = await client.GetAsync("/compat/radarr/api/v3/config/mediamanagement");
		radarrMediaGet.StatusCode.ShouldBe(HttpStatusCode.OK);
		var radarrMedia = await radarrMediaGet.Content.ReadFromJsonAsync<JsonElement>();
		radarrMedia.GetProperty("autoUnmonitorPreviouslyDownloadedMovies").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task ConfigRoute_ShouldRejectUnknownSingletonIdAndInvalidEnum()
	{
		using var client = await _factory.CreateAuthorizedClientAsync();
		using var missing = await client.PutAsJsonAsync("/compat/sonarr/api/v3/config/naming/2", new { id = 2 });
		missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var invalid = await client.PutAsJsonAsync("/compat/radarr/api/v3/config/naming", new { colonReplacement = 99 });
		invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
