using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class CompatActivityRouteTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public CompatActivityRouteTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public async Task ActivityReads_ShouldRequireKeyAndProjectPagedAndArrayContractsFromNativeRows()
	{
		using var anonymous = _factory.CreateClient();
		(await anonymous.GetAsync("/compat/sonarr/api/v3/history")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await anonymous.GetAsync("/compat/radarr/api/v3/calendar")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var client = await _factory.CreateAuthorizedClientAsync();

		var emptyHistory = await GetJsonAsync(client, "/compat/sonarr/api/v3/history?page=1&pageSize=2");
		emptyHistory.GetProperty("records").GetArrayLength().ShouldBe(0);
		emptyHistory.GetProperty("totalRecords").GetInt32().ShouldBe(0);
		emptyHistory.GetProperty("page").GetInt32().ShouldBe(1);
		emptyHistory.GetProperty("pageSize").GetInt32().ShouldBe(2);
		(await GetJsonAsync(client, "/compat/radarr/api/v3/history?page=1&pageSize=2")).GetProperty("records").GetArrayLength().ShouldBe(0);
		(await GetJsonAsync(client, "/compat/sonarr/api/v3/calendar")).ValueKind.ShouldBe(JsonValueKind.Array);
		(await GetJsonAsync(client, "/compat/radarr/api/v3/calendar")).ValueKind.ShouldBe(JsonValueKind.Array);
		(await GetJsonAsync(client, "/compat/sonarr/api/v3/wanted/missing?page=1&pageSize=2")).GetProperty("records").GetArrayLength().ShouldBe(0);
		(await GetJsonAsync(client, "/compat/sonarr/api/v3/wanted/cutoff?page=1&pageSize=2")).GetProperty("records").GetArrayLength().ShouldBe(0);
		(await GetJsonAsync(client, "/compat/radarr/api/v3/wanted/missing?page=1&pageSize=2")).GetProperty("records").GetArrayLength().ShouldBe(0);
		(await GetJsonAsync(client, "/compat/radarr/api/v3/wanted/cutoff?page=1&pageSize=2")).GetProperty("records").GetArrayLength().ShouldBe(0);

		var ids = await SeedActivityAsync();
		var sonarrHistory = await GetJsonAsync(client, "/compat/sonarr/api/v3/history?page=1&pageSize=1");
		sonarrHistory.GetProperty("totalRecords").GetInt32().ShouldBe(2);
		sonarrHistory.GetProperty("records").GetArrayLength().ShouldBe(1);
		var seriesRecord = sonarrHistory.GetProperty("records")[0];
		seriesRecord.GetProperty("eventType").GetString().ShouldBe("downloadFolderImported");
		seriesRecord.GetProperty("episode").GetProperty("id").GetInt32().ShouldBe(ids.EpisodeId);
		seriesRecord.GetProperty("quality").GetProperty("quality").GetProperty("name").GetString().ShouldBe(ids.QualityName);
		seriesRecord.GetProperty("data").GetProperty("source").GetString().ShouldBe("native");
		seriesRecord.GetProperty("languages")[0].GetProperty("name").GetString().ShouldBe("English");
		var radarrHistory = await GetJsonAsync(client, "/compat/radarr/api/v3/history?page=1&pageSize=1");
		radarrHistory.GetProperty("totalRecords").GetInt32().ShouldBe(2);
		radarrHistory.GetProperty("records").GetArrayLength().ShouldBe(1);
		radarrHistory.GetProperty("records")[0].GetProperty("movie").GetProperty("id").GetInt32().ShouldBe(ids.MovieId);
		var seriesHistory = await GetJsonAsync(client, $"/compat/sonarr/api/v3/history/series?seriesId={ids.SeriesId}");
		seriesHistory.ValueKind.ShouldBe(JsonValueKind.Array);
		seriesHistory.GetArrayLength().ShouldBe(2);
		var movieHistory = await GetJsonAsync(client, $"/compat/radarr/api/v3/history/movie?movieId={ids.MovieId}");
		movieHistory.ValueKind.ShouldBe(JsonValueKind.Array);
		movieHistory.GetArrayLength().ShouldBe(2);
		var filteredHistory = await GetJsonAsync(client, $"/compat/sonarr/api/v3/history?seriesId={ids.SeriesId}&episodeId={ids.EpisodeId}&eventType=3&filter=downloadFolderImported&pageSize=1");
		filteredHistory.GetProperty("totalRecords").GetInt32().ShouldBe(2);
		var wrongEventType = await GetJsonAsync(client, "/compat/sonarr/api/v3/history?eventType=1");
		wrongEventType.GetProperty("totalRecords").GetInt32().ShouldBe(0);
		var filteredMovieHistory = await GetJsonAsync(client, $"/compat/radarr/api/v3/history?movieIds={ids.MovieId}");
		filteredMovieHistory.GetProperty("totalRecords").GetInt32().ShouldBe(2);

		var calendar = await GetJsonAsync(client, $"/compat/sonarr/api/v3/calendar?start={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}&end={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}&includeSeries=true&includeEpisodeFile=true&includeEpisodeImages=true");
		calendar.ValueKind.ShouldBe(JsonValueKind.Array);
		calendar.GetArrayLength().ShouldBe(1);
		calendar[0].GetProperty("id").GetInt32().ShouldBe(ids.EpisodeId);
		calendar[0].GetProperty("series").GetProperty("id").GetInt32().ShouldBe(ids.SeriesId);
		calendar[0].GetProperty("episodeFile").ValueKind.ShouldBe(JsonValueKind.Null);
		calendar[0].GetProperty("images").GetArrayLength().ShouldBe(0);
		var unmonitoredCalendar = await GetJsonAsync(client, $"/compat/sonarr/api/v3/calendar?start={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}&end={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}&unmonitored=true");
		unmonitoredCalendar.GetArrayLength().ShouldBe(2);
		var movieCalendar = await GetJsonAsync(client, $"/compat/radarr/api/v3/calendar?start={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}&end={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}");
		movieCalendar.ValueKind.ShouldBe(JsonValueKind.Array);
		movieCalendar.GetArrayLength().ShouldBe(1);
		movieCalendar[0].GetProperty("id").GetInt32().ShouldBe(ids.MovieId);
		var missingEpisodes = await GetJsonAsync(client, "/compat/sonarr/api/v3/wanted/missing?page=1&pageSize=1");
		missingEpisodes.GetProperty("totalRecords").GetInt32().ShouldBe(1);
		missingEpisodes.GetProperty("records")[0].GetProperty("lastSearchTime").ValueKind.ShouldBe(JsonValueKind.Null);
		var missingMovies = await GetJsonAsync(client, "/compat/radarr/api/v3/wanted/missing?page=1&pageSize=1");
		missingMovies.GetProperty("totalRecords").GetInt32().ShouldBe(1);
		missingMovies.GetProperty("records")[0].GetProperty("lastSearchTime").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	private async Task<ActivityIds> SeedActivityAsync()
	{
		return await _factory.WithDbAsync(async db =>
		{
			var quality = QualityResolutionModel.All.First(item => item.Resolution is null);
			var profile = new QualityProfile { Name = "Activity profile", UpgradeAllowed = true, Cutoff = 0, Items = [new QualityProfileItem(quality, true)] };
			var languageProfile = new LanguageProfile { Name = "Activity language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var seriesRoot = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "activity-series"), MediaKind = MediaKind.SERIES };
			var movieRoot = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "activity-movies"), MediaKind = MediaKind.MOVIES };
			db.QualityProfiles.Add(profile);
			db.LanguageProfiles.Add(languageProfile);
			db.RootFolders.AddRange(seriesRoot, movieRoot);
			await db.SaveChangesAsync();
			var series = new Series { TvdbId = 765432101, Title = "Activity Series" };
			var movie = new Movie { TmdbId = 765432101, Title = "Activity Movie", InCinemasDate = DateTime.UtcNow.Date, DigitalReleaseDate = DateTime.UtcNow.Date, PhysicalReleaseDate = DateTime.UtcNow.Date };
			db.Series.Add(series);
			db.Movies.Add(movie);
			await db.SaveChangesAsync();
			var seriesVersion = new MediaVersion { Name = "Default", SeriesId = series.Id, QualityProfileId = profile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = seriesRoot.Id, Path = "Activity Series" };
			var movieVersion = new MediaVersion { Name = "Default", MovieId = movie.Id, QualityProfileId = profile.Id, LanguageProfileId = languageProfile.Id, RootFolderId = movieRoot.Id, Path = "Activity Movie" };
			db.MediaVersions.AddRange(seriesVersion, movieVersion);
			var episode = new Episode { Series = series, SeasonNumber = 1, EpisodeNumber = 2, Title = "An Existing Episode", AirDateUtc = DateTime.UtcNow.Date, Monitored = true };
			var unmonitoredEpisode = new Episode { Series = series, SeasonNumber = 1, EpisodeNumber = 3, Title = "An Unmonitored Episode", AirDateUtc = DateTime.UtcNow.Date, Monitored = false };
			db.Episodes.AddRange(episode, unmonitoredEpisode);
			await db.SaveChangesAsync();
			var eventDate = DateTime.UtcNow;
			db.HistoryEvents.AddRange(
				new HistoryEvent { Type = HistoryEventType.IMPORTED, Series = series, Episode = episode, MediaVersionId = seriesVersion.Id, SourceTitle = "Series release", Quality = new QualityModel(quality, new Revision()), Languages = [Language.ENGLISH], Data = "{\"source\":\"native\"}", Date = eventDate },
				new HistoryEvent { Type = HistoryEventType.IMPORTED, Series = series, Episode = episode, MediaVersionId = seriesVersion.Id, SourceTitle = "Series earlier release", Quality = new QualityModel(quality, new Revision()), Languages = [Language.ENGLISH], Data = "{\"source\":\"native\"}", Date = eventDate.AddSeconds(-1) },
				new HistoryEvent { Type = HistoryEventType.IMPORTED, Movie = movie, MediaVersionId = movieVersion.Id, SourceTitle = "Movie release", Quality = new QualityModel(quality, new Revision()), Languages = [Language.ENGLISH], Data = "{\"source\":\"native\"}", Date = eventDate },
				new HistoryEvent { Type = HistoryEventType.IMPORTED, Movie = movie, MediaVersionId = movieVersion.Id, SourceTitle = "Movie earlier release", Quality = new QualityModel(quality, new Revision()), Languages = [Language.ENGLISH], Data = "{\"source\":\"native\"}", Date = eventDate.AddSeconds(-1) });
			await db.SaveChangesAsync();
			return new ActivityIds(series.Id, episode.Id, movie.Id, quality.Name);
		});
	}

	private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
	}

	private sealed record ActivityIds(int SeriesId, int EpisodeId, int MovieId, string QualityName);
}
