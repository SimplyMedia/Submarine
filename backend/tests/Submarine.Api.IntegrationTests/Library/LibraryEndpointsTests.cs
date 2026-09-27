using Submarine.Core.Quality;
using System.Net;
using Submarine.Contracts.Metadata;
using Submarine.Core.Commands;
using Xunit;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests.Library;

/// <summary>
///     End to end flows over the library endpoints against a stubbed Metadata service.
/// </summary>
public sealed class LibraryEndpointsTests
{
	[Fact]
	public async Task Root_Folders_ShouldValidateExistenceAndDuplicates()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();

		var missing = await client.PostAsJsonAsync("/api/v1/root-folders", new
		{
			path = Path.Combine(Path.GetTempPath(), "does-not-exist-submarine"),
			mediaKind = "SERIES"
		});
		missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var created = await client.PostAsJsonAsync("/api/v1/root-folders", new { path = root, mediaKind = "SERIES" });
			created.StatusCode.ShouldBe(HttpStatusCode.Created);

			var duplicate = await client.PostAsJsonAsync("/api/v1/root-folders", new { path = root, mediaKind = "SERIES" });
			duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Add_Series_ShouldCreateSeasonsEpisodesVersionAndFolder()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			factory.Metadata.Series[LibraryTestSupport.TvdbId] = LibraryTestSupport.SeriesFixture(LibraryTestSupport.TvdbId, "Test Series", DateTime.UtcNow);
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);

			var response = await client.PostAsJsonAsync("/api/v1/series", new
			{
				tvdbId = LibraryTestSupport.TvdbId,
				metadataProvider = "TVDB",
				rootFolderId = rootId,
				monitorOption = "ALL",
				monitorSpecials = false,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			response.StatusCode.ShouldBe(HttpStatusCode.Created);
			var detail = await response.Content.ReadFromJsonAsync<SeriesDetailDto>();
			detail!.ShouldNotBeNull();
			detail.Series.TvdbId.ShouldBe(LibraryTestSupport.TvdbId);
			detail.Series.Statistics.TotalEpisodeCount.ShouldBe(6);
			detail.Series.Statistics.EpisodeCount.ShouldBe(5);
			detail.Series.Versions.ShouldHaveSingleItem();
			detail.Series.Versions[0].Path.ShouldBe("Test Series");

			Directory.Exists(Path.Combine(root, "Test Series")).ShouldBeTrue();

			var folderCheck = await client.GetFromJsonAsync<SeriesFolderDto>($"/api/v1/series/{detail.Series.Id}/folder");
			folderCheck!.Exists.ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Add_Series_ShouldReturn409_WhenTvdbIdExists()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var duplicate = await client.PostAsJsonAsync("/api/v1/series", new
			{
				tvdbId = LibraryTestSupport.TvdbId,
				metadataProvider = "TVDB",
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Series_Editor_ShouldBulkUpdateMonitoredAndTags()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var tagId = await CreateTagAsync(client, "editor-test");
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var secondId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, LibraryTestSupport.SecondTvdbId, "Second Series");

			var response = await client.PutAsJsonAsync("/api/v1/series/editor", new
			{
				ids = new[] { seriesId, secondId },
				monitored = false,
				tags = new { mode = "add", tagIds = new[] { tagId } }
			});
			response.StatusCode.ShouldBe(HttpStatusCode.OK);

			var list = (await client.GetFromJsonAsync<PagedDto<SeriesListItemDto>>("/api/v1/series?monitored=false"))!.Items;
			list!.Count.ShouldBe(2);
			list.ShouldContain(x => x.Id == seriesId && x.TagIds.Contains(tagId));
			list.ShouldContain(x => x.Id == secondId && x.TagIds.Contains(tagId));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Season_Monitor_ShouldUpdateSeasonAndItsEpisodes()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var monitor = await client.PutAsJsonAsync($"/api/v1/series/{seriesId}/seasons/1/monitor", new { monitored = false });
			monitor.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var episodes = await client.GetFromJsonAsync<List<EpisodeDto>>($"/api/v1/episodes?seriesId={seriesId}");
			episodes!.Where(x => x.SeasonNumber == 1).All(x => !x.Monitored).ShouldBeTrue();
			episodes!.Where(x => x.SeasonNumber == 2).All(x => x.Monitored).ShouldBeTrue();

			var seasonPass = await client.PutAsJsonAsync($"/api/v1/series/{seriesId}/seasons/season-pass", new
			{
				monitoringOption = "NONE"
			});
			seasonPass.StatusCode.ShouldBe(HttpStatusCode.NoContent);
			var after = await client.GetFromJsonAsync<List<EpisodeDto>>($"/api/v1/episodes?seriesId={seriesId}");
			after!.All(x => !x.Monitored).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Wanted_Missing_ShouldListAiredMonitoredEpisodesWithoutFiles()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var response = (await client.GetFromJsonAsync<PagedDto<WantedItemDto>>("/api/v1/wanted/missing"))!.Items;
			response!.ShouldNotBeNull();
			response.Count.ShouldBe(3);

			var episodeId = await LibraryTestSupport.EpisodeIdAsync(client, seriesId, 1, 1);
			var episode = response.Single(x => x.Type == "episode" && x.Id == episodeId);
			episode.Title.ShouldBe("Test Series");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Calendar_Feed_ShouldRejectWrongToken_AndServeIcsWithCorrectToken()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var feedToken = await factory.WithDbAsync(async db => (await db.GeneralConfig.SingleAsync()).FeedToken);

			var noToken = await client.GetAsync("/api/v1/calendar/feed.ics");
			noToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

			var wrongToken = await client.GetAsync("/api/v1/calendar/feed.ics?token=wrong-token");
			wrongToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

			var correct = await client.GetAsync($"/api/v1/calendar/feed.ics?token={feedToken}&pastDays=7&futureDays=30");
			correct.StatusCode.ShouldBe(HttpStatusCode.OK);
			correct.Content.Headers.ContentType!.MediaType.ShouldBe("text/calendar");
			var body = await correct.Content.ReadAsStringAsync();
			body.ShouldContain("BEGIN:VCALENDAR");
			body.ShouldContain("Test Series");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Calendar_ShouldReturnEpisodesWithinRange()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var start = DateTime.UtcNow.AddDays(-420).ToString("yyyy-MM-dd");
			var end = DateTime.UtcNow.AddDays(-300).ToString("yyyy-MM-dd");
			var response = await client.GetFromJsonAsync<List<CalendarEventDto>>(
				$"/api/v1/calendar?start={start}&end={end}");

			response!.ShouldNotBeNull();
			response.ShouldContain(x => x.Type == "episode" && x.Title == "Test Series" && x.Kind == "aired");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Calendar_ShouldReportHasFileAndDownloading()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var withFileEpisodeId = await LibraryTestSupport.EpisodeIdAsync(client, seriesId, 1, 1);
			var downloadingEpisodeId = await LibraryTestSupport.EpisodeIdAsync(client, seriesId, 1, 2);

			await factory.WithDbAsync(async db =>
			{
				var version = await db.MediaVersions.FirstAsync(v => v.SeriesId == seriesId);
				var episode = await db.Episodes.FirstAsync(e => e.Id == withFileEpisodeId);
				db.EpisodeFiles.Add(new EpisodeFile
				{
					SeriesId = seriesId,
					MediaVersionId = version.Id,
					RelativePath = "episode.mkv",
					Size = 1000,
					DateAdded = DateTime.UtcNow,
					Episodes = [episode]
				});

				var downloadClient = new DownloadClient { Name = "Test Client", Type = DownloadClientType.QBITTORRENT, Enable = true, SettingsJson = "{}" };
				db.DownloadClients.Add(downloadClient);
				await db.SaveChangesAsync();

				db.TrackedDownloads.Add(new TrackedDownload
				{
					DownloadClientId = downloadClient.Id,
					DownloadId = "abc",
					Title = "Downloading Release",
					Protocol = Protocol.BITTORRENT,
					Status = TrackedDownloadStatus.DOWNLOADING,
					State = TrackedDownloadState.DOWNLOADING,
					SeriesId = seriesId,
					EpisodeIds = [downloadingEpisodeId]
				});
				await db.SaveChangesAsync();
				return true;
			});

			var start = DateTime.UtcNow.AddDays(-420).ToString("yyyy-MM-dd");
			var end = DateTime.UtcNow.AddDays(-300).ToString("yyyy-MM-dd");
			var response = await client.GetFromJsonAsync<List<CalendarEventDto>>(
				$"/api/v1/calendar?start={start}&end={end}");

			response!.ShouldNotBeNull();
			response.ShouldContain(x => x.Id == withFileEpisodeId && x.HasFile && !x.Downloading);
			response.ShouldContain(x => x.Id == downloadingEpisodeId && x.Downloading && !x.HasFile);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Episodes_Monitor_ShouldPersistFlags()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var episodeId = await LibraryTestSupport.EpisodeIdAsync(client, seriesId, 1, 1);

			var response = await client.PutAsJsonAsync("/api/v1/episodes/monitor", new
			{
				episodeIds = new[] { episodeId },
				monitored = false
			});
			response.StatusCode.ShouldBe(HttpStatusCode.OK);

			var single = await client.PutAsJsonAsync($"/api/v1/episodes/{episodeId}", new { monitored = true });
			single.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			var episodes = await client.GetFromJsonAsync<List<EpisodeDto>>($"/api/v1/episodes?seriesId={seriesId}");
			episodes!.Single(x => x.Id == episodeId).Monitored.ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Media_Versions_ShouldRefuseDeletingTheLastVersion()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var detail = await client.GetFromJsonAsync<SeriesDetailDto>($"/api/v1/series/{seriesId}");
			var versionId = detail!.Series.Versions[0].Id;

			var refused = await client.DeleteAsync($"/api/v1/media-versions/{versionId}");
			refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);

			var created = await client.PostAsJsonAsync($"/api/v1/series/{seriesId}/versions", new
			{
				name = "4K",
				qualityProfileId = 1,
				languageProfileId = 1,
				rootFolderId = rootId
			});
			created.StatusCode.ShouldBe(HttpStatusCode.Created);
			var second = await created.Content.ReadFromJsonAsync<VersionDto>();
			second!.Name.ShouldBe("4K");

			var deleted = await client.DeleteAsync($"/api/v1/media-versions/{second.Id}");
			deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Movie_MediaVersion_ShouldRefuseDeletingLastVersionWhenOtherMoviesExist()
	{
		await using var factory = new LibraryApiFactory();
		LibraryTestSupport.MovieFixtures(factory.Metadata, DateTime.UtcNow);
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root, "MOVIES");
			var added = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.TmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			added.EnsureSuccessStatusCode();
			var unrelated = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.SecondTmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			unrelated.EnsureSuccessStatusCode();
			var movieId = await factory.WithDbAsync(db => db.Movies.Where(x => x.TmdbId == LibraryTestSupport.TmdbId).Select(x => x.Id).SingleAsync());
			var version = await factory.WithDbAsync(db =>
				db.MediaVersions.Where(x => x.MovieId == movieId).Select(x => new { x.Id, x.Path }).FirstAsync());

			var folder = Path.Combine(root, version.Path);
			Directory.CreateDirectory(folder);
			var refused = await client.DeleteAsync($"/api/v1/media-versions/{version.Id}?deleteFiles=true");

			refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
			Directory.Exists(folder).ShouldBeTrue();
			(await factory.WithDbAsync(db => db.MediaVersions.AnyAsync(x => x.Id == version.Id))).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Media_Version_Update_ShouldRejectPathAndRootMoveTogether()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		var newRoot = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var newRootId = await LibraryTestSupport.CreateRootFolderAsync(client, newRoot);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var series = await client.GetFromJsonAsync<SeriesDetailDto>($"/api/v1/series/{seriesId}");
			var version = series!.Series.Versions.Single();

			var response = await client.PutAsJsonAsync($"/api/v1/media-versions/{version.Id}", new
			{
				path = "renamed",
				rootFolderId = newRootId,
				moveFiles = true
			});

			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			var persisted = await factory.WithDbAsync(db => db.MediaVersions.Where(x => x.Id == version.Id)
				.Select(x => new { x.RootFolderId, x.Path }).SingleAsync());
			persisted.RootFolderId.ShouldBe(rootId);
			persisted.Path.ShouldBe(version.Path);
			(await factory.WithDbAsync(db => db.Commands.AnyAsync(x => x.Name == "MoveMediaVersion"))).ShouldBeFalse();
		}
		finally
		{
			Directory.Delete(root, true);
			Directory.Delete(newRoot, true);
		}
	}

	[Fact]
	public async Task Movie_MediaVersion_DeleteAndFacadeRead_ShouldReturnConflictForInvalidCompatibilityBinding()
	{
		await using var factory = new LibraryApiFactory();
		LibraryTestSupport.MovieFixtures(factory.Metadata, DateTime.UtcNow);
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root, "MOVIES");
			async Task<int> AddMovieAsync(int tmdbId)
			{
				var response = await client.PostAsJsonAsync("/api/v1/movies", new
				{
					tmdbId,
					rootFolderId = rootId,
					versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
				});
				response.EnsureSuccessStatusCode();
				return await factory.WithDbAsync(db => db.Movies.Where(x => x.TmdbId == tmdbId).Select(x => x.Id).SingleAsync());
			}

			var movieId = await AddMovieAsync(LibraryTestSupport.TmdbId);
			var otherMovieId = await AddMovieAsync(LibraryTestSupport.SecondTmdbId);
			var extra = await client.PostAsJsonAsync($"/api/v1/movies/{movieId}/versions", new
			{
				name = "alternate",
				qualityProfileId = 1,
				languageProfileId = 1,
				rootFolderId = rootId
			});
			extra.EnsureSuccessStatusCode();
			var versionIds = await factory.WithDbAsync(async db =>
			{
				var own = await db.MediaVersions.Where(x => x.MovieId == movieId).Select(x => x.Id).ToListAsync();
				var foreign = await db.MediaVersions.Where(x => x.MovieId == otherMovieId).Select(x => x.Id).SingleAsync();
				db.CompatLibraryBindings.Add(new CompatLibraryBinding
				{
					Facade = "radarr",
					MovieId = movieId,
					MediaVersionId = foreign
				});
				await db.SaveChangesAsync();
				return own;
			});
			var facadeRead = await client.GetAsync($"/compat/radarr/api/v3/movie/{movieId}");
			facadeRead.StatusCode.ShouldBe(HttpStatusCode.Conflict);


			var response = await client.DeleteAsync($"/api/v1/media-versions/{versionIds[0]}");

			response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
			(await factory.WithDbAsync(db => db.MediaVersions.AnyAsync(x => x.Id == versionIds[0]))).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Delete_Series_WithFiles_ShouldRemoveFolderAndAddExclusion()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var response = await client.DeleteAsync($"/api/v1/series/{seriesId}?deleteFiles=true&addImportListExclusion=true");
			response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

			Directory.Exists(Path.Combine(root, "Test Series")).ShouldBeFalse();
			(await client.GetAsync($"/api/v1/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

			var exclusions = await client.GetFromJsonAsync<List<ExclusionDto>>("/api/v1/import-list-exclusions");
			exclusions!.ShouldContain(x => x.TvdbId == LibraryTestSupport.TvdbId && x.Title == "Test Series");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Tags_Detail_ShouldListUsingSeries()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var tagId = await CreateTagAsync(client, "detail-test");
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var update = await client.PutAsJsonAsync($"/api/v1/series/{seriesId}", new
			{
				tagIds = new[] { tagId }
			});
			update.StatusCode.ShouldBe(HttpStatusCode.OK);

			var detail = await client.GetFromJsonAsync<TagDetailDto>($"/api/v1/tags/{tagId}/detail");
			detail!.SeriesIds.ShouldContain(seriesId);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Movie_And_Collection_AddMissing_ShouldAddMoviesWithCollectionSettings()
	{
		await using var factory = new LibraryApiFactory();
		LibraryTestSupport.MovieFixtures(factory.Metadata, DateTime.UtcNow);
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root, "MOVIES");

			var added = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.TmdbId,
				rootFolderId = rootId,
				minimumAvailability = "ANNOUNCED",
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			added.StatusCode.ShouldBe(HttpStatusCode.Created);

			var duplicate = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.TmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

			// Adding a movie with a TMDB collection auto-creates the Collection row, seeded from
			// the movie's first version and minimum availability, unmonitored until opted in.
			var collections = await client.GetFromJsonAsync<List<CollectionDto>>("/api/v1/collections");
			var collection = collections!.Single(x => x.TmdbCollectionId == LibraryTestSupport.CollectionId);
			collection.Id.ShouldNotBeNull();
			collection.Title.ShouldBe("Test Collection");
			collection.RootFolderId.ShouldBe(rootId);
			collection.QualityProfileId.ShouldBe(1);
			collection.LanguageProfileId.ShouldBe(1);
			collection.MinimumAvailability.ShouldBe("ANNOUNCED");
			collection.Monitored.ShouldBeFalse();

			// MissingCount reflects collection movies from the metadata provider not yet in the
			// library: SecondTmdbId is in the collection's fixture but not added yet.
			collection.MissingCount.ShouldBe(1);
			var refetched = await client.GetFromJsonAsync<CollectionDto>($"/api/v1/collections/{collection.Id}");
			refetched!.MissingCount.ShouldBe(1);

			var addMissing = await client.PostAsJsonAsync($"/api/v1/collections/{collection.Id}/add-missing", new { });
			addMissing.StatusCode.ShouldBe(HttpStatusCode.OK);
			var result = await addMissing.Content.ReadFromJsonAsync<AddMissingDto>();
			result!.Added.ShouldBe(1);

			var afterAddMissing = await client.GetFromJsonAsync<CollectionDto>($"/api/v1/collections/{collection.Id}");
			afterAddMissing!.MissingCount.ShouldBe(0);

			var movies = (await client.GetFromJsonAsync<PagedDto<MovieListItemDto>>("/api/v1/movies"))!.Items;
			movies!.Count.ShouldBe(2);
			movies.ShouldContain(x => x.TmdbId == LibraryTestSupport.SecondTmdbId);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Movie_Detail_ShouldIncludeFileSceneNameEditionAndMediaInfo()
	{
		await using var factory = new LibraryApiFactory();
		LibraryTestSupport.MovieFixtures(factory.Metadata, DateTime.UtcNow);
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root, "MOVIES");
			var added = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.TmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			added.StatusCode.ShouldBe(HttpStatusCode.Created);
			var movie = await added.Content.ReadFromJsonAsync<MovieDetailDto>();
			var movieId = movie!.Movie.Id;

			await factory.WithDbAsync(async db =>
			{
				var version = await db.MediaVersions.FirstAsync(v => v.MovieId == movieId);
				db.MovieFiles.Add(new MovieFile
				{
					MovieId = movieId,
					MediaVersionId = version.Id,
					RelativePath = "movie.mkv",
					Size = 5000,
					DateAdded = DateTime.UtcNow,
					ReleaseGroup = "GROUP",
					SceneName = "Test.Movie.2020.1080p.BluRay.x264-GROUP",
					Edition = "Directors Cut",
					MediaInfo = new MediaInfoModel("h264", "aac", 6, "HDR10", 1920, 1080, 120, 10, null, null)
				});
				await db.SaveChangesAsync();
				return true;
			});

			var detail = await client.GetFromJsonAsync<MovieDetailDto>($"/api/v1/movies/{movieId}");
			var file = detail!.Files.Single();
			file.SceneName.ShouldBe("Test.Movie.2020.1080p.BluRay.x264-GROUP");
			file.Edition.ShouldBe("Directors Cut");
			file.MediaInfo.ShouldNotBeNull();
			file.MediaInfo!.VideoCodec.ShouldBe("h264");
			file.MediaInfo.Width.ShouldBe(1920);
			file.MediaInfo.Height.ShouldBe(1080);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Movies_ShouldSortByReleaseAndSizeAliases()
	{
		await using var factory = new LibraryApiFactory();
		LibraryTestSupport.MovieFixtures(factory.Metadata, DateTime.UtcNow);
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root, "MOVIES");
			var first = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.TmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			first.StatusCode.ShouldBe(HttpStatusCode.Created);
			var second = await client.PostAsJsonAsync("/api/v1/movies", new
			{
				tmdbId = LibraryTestSupport.SecondTmdbId,
				rootFolderId = rootId,
				versions = new[] { new { name = "main", qualityProfileId = 1, languageProfileId = 1 } }
			});
			second.StatusCode.ShouldBe(HttpStatusCode.Created);

			// TmdbId has an earlier DigitalReleaseDate than SecondTmdbId (see MovieFixtures).
			var ascending = (await client.GetFromJsonAsync<PagedDto<MovieListItemDto>>(
				"/api/v1/movies?sortKey=digitalRelease&sortDirection=asc"))!.Items;
			ascending!.Select(x => x.TmdbId).ShouldBe([LibraryTestSupport.TmdbId, LibraryTestSupport.SecondTmdbId]);

			var descending = (await client.GetFromJsonAsync<PagedDto<MovieListItemDto>>(
				"/api/v1/movies?sortKey=digitalRelease&sortDirection=desc"))!.Items;
			descending!.Select(x => x.TmdbId).ShouldBe([LibraryTestSupport.SecondTmdbId, LibraryTestSupport.TmdbId]);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Refresh_ShouldDiffEpisodesAndAddNewSeasons()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);

			var now = DateTime.UtcNow;
			var updated = LibraryTestSupport.SeriesFixture(LibraryTestSupport.TvdbId, "Test Series", now);
			var seasons = updated.Seasons.ToList();
			seasons.Add(new SeasonResource(3, "Season 3", 1));
			var episodes = updated.Episodes.ToList();
			episodes[3] = episodes[3] with { Title = "Renamed Episode" };
			episodes.Add(new Contracts.Metadata.EpisodeResource(
				10203,
				null,
				"Brand New Episode",
				"Fresh from the provider",
				DateOnly.FromDateTime(now.AddDays(-1)),
				DateOnly.FromDateTime(now.AddDays(-1)),
				42,
				[new Contracts.Metadata.EpisodeNumber(Contracts.Metadata.EpisodeOrdering.AIRED, 3, 1, 6)],
				null));
			factory.Metadata.Series[LibraryTestSupport.TvdbId] = updated with { Seasons = seasons, Episodes = episodes };

			await LibraryTestSupport.RunHandlerAsync(factory, new RefreshSeriesCommand(seriesId));

			var episodesAfter = await client.GetFromJsonAsync<List<EpisodeDto>>($"/api/v1/episodes?seriesId={seriesId}");
			episodesAfter!.Count.ShouldBe(7);
			episodesAfter!.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 3).Title.ShouldBe("Renamed Episode");
			episodesAfter.ShouldContain(x => x.SeasonNumber == 3 && x.EpisodeNumber == 1 && x.Monitored);

			var detail = await client.GetFromJsonAsync<SeriesDetailDto>($"/api/v1/series/{seriesId}");
			detail!.Series.Statistics.TotalEpisodeCount.ShouldBe(7);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Refresh_ShouldQueueRename_WhenPlaceholderNamedFileGetsItsTitle()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			var episodeId = await LibraryTestSupport.EpisodeIdAsync(client, seriesId, 1, 3);
			var fileId = await factory.WithDbAsync(async db =>
			{
				var episode = await db.Episodes.SingleAsync(x => x.Id == episodeId);
				episode.Title = null;
				var file = new EpisodeFile
				{
					SeriesId = seriesId,
					MediaVersionId = (await db.MediaVersions.FirstAsync(x => x.SeriesId == seriesId)).Id,
					RelativePath = "Season 01/Test Series - S01E03 - Episode 3.mkv",
					Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
					NamedFromPlaceholder = true
				};
				file.Episodes.Add(episode);
				db.EpisodeFiles.Add(file);
				await db.SaveChangesAsync();
				return file.Id;
			});

			var updated = LibraryTestSupport.SeriesFixture(LibraryTestSupport.TvdbId, "Test Series", DateTime.UtcNow);
			var episodes = updated.Episodes.ToList();
			episodes[3] = episodes[3] with { Title = "The Real Title" };
			factory.Metadata.Series[LibraryTestSupport.TvdbId] = updated with { Episodes = episodes };

			await LibraryTestSupport.RunHandlerAsync(factory, new RefreshSeriesCommand(seriesId));

			var renames = await factory.WithDbAsync(db => db.Commands.Where(x => x.Name == "RenameSeries").Select(x => x.Body).ToListAsync());
			renames.ShouldHaveSingleItem().ShouldContain($"[{fileId}]");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task Import_List_Sync_ShouldAddNewSeries_AndSkipExistingAndExcluded()
	{
		await using var factory = new LibraryApiFactory();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			var sonarrJson = """
				[
					{"title": "Test Series", "tvdbId": 107151, "year": 2024},
					{"title": "Fresh Series", "tvdbId": 555555, "year": 2026},
					{"title": "Blocked Series", "tvdbId": 666666, "year": 2025}
				]
				""";
			factory.ImportListHttpHandler = new StubHttpHandler(sonarrJson);
			var client = await factory.CreateAuthorizedClientAsync();
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			await LibraryTestSupport.AddSeriesAsync(factory, client, rootId);
			factory.Metadata.Series[555555] = LibraryTestSupport.SeriesFixture(555555, "Fresh Series", DateTime.UtcNow);

			var created = await client.PostAsJsonAsync("/api/v1/import-lists", new
			{
				name = "Sonarr instance",
				type = "SONARR",
				enable = true,
				enableAutomaticAdd = true,
				mediaKind = "SERIES",
				qualityProfileId = 1,
				languageProfileId = 1,
				rootFolderId = rootId,
				settings = new { baseUrl = "http://sonarr.example", apiKey = "key" }
			});
			created.StatusCode.ShouldBe(HttpStatusCode.Created);
			var list = await created.Content.ReadFromJsonAsync<ImportListDto>();

			await client.PostAsJsonAsync("/api/v1/import-list-exclusions", new
			{
				tvdbId = 666666,
				title = "Blocked Series"
			});

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand(list!.Id));

			var series = (await client.GetFromJsonAsync<PagedDto<SeriesListItemDto>>("/api/v1/series"))!.Items;
			series!.Count.ShouldBe(2);
			series.ShouldContain(x => x.TvdbId == 555555);

			var preview = await client.GetAsync($"/api/v1/import-lists/{list.Id}/preview");
			preview.StatusCode.ShouldBe(HttpStatusCode.OK);
			var previewItems = await preview.Content.ReadFromJsonAsync<List<PreviewItemDto>>();
			previewItems!.Count.ShouldBe(3);
			previewItems!.Count(x => x.Status == "exists").ShouldBe(2, "107151 pre-existed and 555555 was just synced in");
			previewItems!.ShouldContain(x => x.Status == "excluded");

			var test = await client.PostAsJsonAsync($"/api/v1/import-lists/{list.Id}/test", new { });
			test.StatusCode.ShouldBe(HttpStatusCode.OK);
			var testResult = await test.Content.ReadFromJsonAsync<ImportListTestResultDto>();
			testResult!.Success.ShouldBeTrue();
			testResult.ItemCount.ShouldBe(3);

			var schema = await client.GetFromJsonAsync<List<SchemaDto>>("/api/v1/import-lists/schema");
			schema!.ShouldContain(x => x.Type == "SONARR" && x.Fields.Any(f => f.Name == "baseUrl"));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldUnmonitorItemsNotOnAnyList_WhenLevelIsKeepAndUnmonitor()
	{
		await using var factory = new LibraryApiFactory();
		const int onListTvdbId = 810001;
		const int offListTvdbId = 810002;
		factory.ImportListHttpHandler = new StubHttpHandler($$"""[{"tvdbId": {{onListTvdbId}}, "title": "On List Show"}]""");
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var onListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, onListTvdbId, "On List Show");
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show");

			await SetCleanLibraryLevelAsync(client, "KEEP_AND_UNMONITOR");
			await CreateAutomaticAddCustomListAsync(client, "SERIES", rootId);

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			var series = (await client.GetFromJsonAsync<PagedDto<SeriesListItemDto>>("/api/v1/series"))!.Items!;
			series.Single(x => x.Id == onListId).Monitored.ShouldBeTrue("the item is still on the list");
			series.Single(x => x.Id == offListId).Monitored.ShouldBeFalse("the item is no longer on any automatic-add list");
			Directory.Exists(Path.Combine(root, "On List Show")).ShouldBeTrue();
			Directory.Exists(Path.Combine(root, "Off List Show")).ShouldBeTrue("unmonitor keeps the item and its files");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldRemoveButKeepFiles_WhenLevelIsRemoveAndKeep()
	{
		await using var factory = new LibraryApiFactory();
		const int onListTvdbId = 810003;
		const int offListTvdbId = 810004;
		factory.ImportListHttpHandler = new StubHttpHandler($$"""[{"tvdbId": {{onListTvdbId}}, "title": "On List Show2"}]""");
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var onListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, onListTvdbId, "On List Show2");
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show2");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_KEEP");
			await CreateAutomaticAddCustomListAsync(client, "SERIES", rootId);

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{onListId}")).StatusCode.ShouldBe(HttpStatusCode.OK, "the item is still on the list");
			(await client.GetAsync($"/api/v1/series/{offListId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			Directory.Exists(Path.Combine(root, "On List Show2")).ShouldBeTrue();
			Directory.Exists(Path.Combine(root, "Off List Show2")).ShouldBeTrue("remove and keep must not delete the files");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldRemoveAndDeleteFiles_WhenLevelIsRemoveAndDelete()
	{
		await using var factory = new LibraryApiFactory();
		const int onListTvdbId = 810005;
		const int offListTvdbId = 810006;
		factory.ImportListHttpHandler = new StubHttpHandler($$"""[{"tvdbId": {{onListTvdbId}}, "title": "On List Show3"}]""");
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var onListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, onListTvdbId, "On List Show3");
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show3");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_DELETE");
			await CreateAutomaticAddCustomListAsync(client, "SERIES", rootId);

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{onListId}")).StatusCode.ShouldBe(HttpStatusCode.OK, "the item is still on the list");
			(await client.GetAsync($"/api/v1/series/{offListId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
			Directory.Exists(Path.Combine(root, "On List Show3")).ShouldBeTrue();
			Directory.Exists(Path.Combine(root, "Off List Show3")).ShouldBeFalse("remove and delete must delete the files");
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldChangeNothing_WhenLevelIsLogOnly()
	{
		await using var factory = new LibraryApiFactory();
		const int onListTvdbId = 810007;
		const int offListTvdbId = 810008;
		factory.ImportListHttpHandler = new StubHttpHandler($$"""[{"tvdbId": {{onListTvdbId}}, "title": "On List Show4"}]""");
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var onListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, onListTvdbId, "On List Show4");
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show4");

			await SetCleanLibraryLevelAsync(client, "LOG_ONLY");
			await CreateAutomaticAddCustomListAsync(client, "SERIES", rootId);

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			var series = (await client.GetFromJsonAsync<PagedDto<SeriesListItemDto>>("/api/v1/series"))!.Items!;
			series.Single(x => x.Id == onListId).Monitored.ShouldBeTrue();
			series.Single(x => x.Id == offListId).Monitored.ShouldBeTrue("log only never changes monitored state or removes anything");
			Directory.Exists(Path.Combine(root, "Off List Show4")).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldNotRun_WhenAnyListFetchFailed()
	{
		await using var factory = new LibraryApiFactory();
		const int offListTvdbId = 810009;
		factory.ImportListHttpHandler = new FailingHttpHandler();
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show5");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_DELETE");
			await CreateAutomaticAddCustomListAsync(client, "SERIES", rootId);

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{offListId}")).StatusCode.ShouldBe(HttpStatusCode.OK, "a failed list fetch must never trigger clean library");
			Directory.Exists(Path.Combine(root, "Off List Show5")).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldNotRun_WhenNoAutomaticAddListSynced()
	{
		await using var factory = new LibraryApiFactory();
		const int offListTvdbId = 810010;
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var offListId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, offListTvdbId, "Off List Show6");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_DELETE");
			// No import lists configured at all, so no automatic-add list ever synced.

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{offListId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
			Directory.Exists(Path.Combine(root, "Off List Show6")).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldNotRun_WhenAutomaticAddListIsSkipped()
	{
		await using var factory = new LibraryApiFactory
		{
			ImportListHttpHandler = new ImportListPathResponseHandler(new Dictionary<string, string>
			{
				["/active.json"] = """[{"tvdbId": 810011, "title": "Active List Show"}]""",
				["/skipped.json"] = """[{"tvdbId": 810012, "title": "Skipped List Show"}]"""
			})
		};
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var activeId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, 810011, "Active List Show");
			var skippedId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, 810012, "Skipped List Show");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_DELETE");
			await CreateCustomListAsync(client, "Active list", "SERIES", rootId, true, "https://example.com/active.json");
			var skippedListId = await CreateCustomListAsync(client, "Skipped list", "SERIES", rootId, true, "https://example.com/skipped.json");
			await factory.WithDbAsync(async db =>
			{
				db.ImportListStatuses.Add(new ImportListStatus
				{
					ImportListId = skippedListId,
					DisabledUntil = DateTime.UtcNow.AddDays(1)
				});
				await db.SaveChangesAsync();
				return true;
			});

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{activeId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
			(await client.GetAsync($"/api/v1/series/{skippedId}")).StatusCode.ShouldBe(HttpStatusCode.OK,
				"a skipped automatic-add list must prevent clean library from treating its titles as absent");
			Directory.Exists(Path.Combine(root, "Skipped List Show")).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportListSync_CleanLibrary_ShouldKeepItemsFromEnabledNonAutomaticAddLists()
	{
		await using var factory = new LibraryApiFactory
		{
			ImportListHttpHandler = new ImportListPathResponseHandler(new Dictionary<string, string>
			{
				["/automatic.json"] = "[]",
				["/report-only.json"] = """[{"tvdbId": 810013, "title": "Report Only Show"}]"""
			})
		};
		var client = await factory.CreateAuthorizedClientAsync();
		var root = LibraryTestSupport.CreateTempRoot();
		try
		{
			await EnableEmptyFolderCreationAsync(factory);
			var rootId = await LibraryTestSupport.CreateRootFolderAsync(client, root);
			var seriesId = await LibraryTestSupport.AddSeriesAsync(factory, client, rootId, 810013, "Report Only Show");

			await SetCleanLibraryLevelAsync(client, "REMOVE_AND_DELETE");
			await CreateCustomListAsync(client, "Automatic list", "SERIES", rootId, true, "https://example.com/automatic.json");
			await CreateCustomListAsync(client, "Report-only list", "SERIES", rootId, false, "https://example.com/report-only.json");

			await LibraryTestSupport.RunHandlerAsync(factory, new ImportListSyncCommand());

			(await client.GetAsync($"/api/v1/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK,
				"items on any enabled list must count as covered, even when automatic add is off");
			Directory.Exists(Path.Combine(root, "Report Only Show")).ShouldBeTrue();
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	private static async Task<int> CreateCustomListAsync(
		HttpClient client,
		string name,
		string mediaKind,
		int rootFolderId,
		bool enableAutomaticAdd,
		string url)
	{
		var response = await client.PostAsJsonAsync("/api/v1/import-lists", new
		{
			name,
			type = "CUSTOM",
			enable = true,
			enableAutomaticAdd,
			mediaKind,
			qualityProfileId = 1,
			languageProfileId = 1,
			rootFolderId,
			settings = new { url }
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		return (await response.Content.ReadFromJsonAsync<ImportListDto>())!.Id;
	}

	private static async Task SetCleanLibraryLevelAsync(HttpClient client, string level)
	{
		var response = await client.PutAsJsonAsync("/api/v1/config/import-list", new { cleanLibraryLevel = level });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private static async Task CreateAutomaticAddCustomListAsync(HttpClient client, string mediaKind, int rootFolderId)
	{
		var response = await client.PostAsJsonAsync("/api/v1/import-lists", new
		{
			name = "Custom list",
			type = "CUSTOM",
			enable = true,
			enableAutomaticAdd = true,
			mediaKind,
			qualityProfileId = 1,
			languageProfileId = 1,
			rootFolderId,
			settings = new { url = "https://example.com/list.json" }
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	private static async Task<int> CreateTagAsync(HttpClient client, string label)
	{
		var response = await client.PostAsJsonAsync("/api/v1/tags", new { label });
		response.EnsureSuccessStatusCode();
		var tag = await response.Content.ReadFromJsonAsync<TagDto>();
		return tag!.Id;
	}

	private static async Task EnableEmptyFolderCreationAsync(LibraryApiFactory factory)
		=> await factory.WithDbAsync(async db =>
		{
			var config = await db.MediaManagementConfig.SingleAsync();
			config.CreateEmptySeriesFolders = true;
			await db.SaveChangesAsync();
			return true;
		});
}

/// <summary>Minimal exclusion response.</summary>
/// <param name="Id">Id.</param>
/// <param name="TvdbId">TVDB id.</param>
/// <param name="Title">Title.</param>
public sealed record ExclusionDto(int Id, int? TvdbId, string Title);

/// <summary>Minimal tag response.</summary>
/// <param name="Id">Id.</param>
/// <param name="Label">Label.</param>
public sealed record TagDto(int Id, string Label);

/// <summary>Minimal import list response.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name.</param>
/// <param name="Type">Type.</param>
public sealed record ImportListDto(int Id, string Name, string Type);

/// <summary>Minimal add-missing response.</summary>
/// <param name="Added">Movies added.</param>
public sealed record AddMissingDto(int Added);

/// <summary>Minimal movie list item response.</summary>
/// <param name="Id">Id.</param>
/// <param name="TmdbId">TMDB id.</param>
public sealed record MovieListItemDto(int Id, int TmdbId);

/// <summary>Minimal movie detail response.</summary>
/// <param name="Movie">List item part.</param>
/// <param name="Files">File summaries.</param>
public sealed record MovieDetailDto(MovieListItemDto Movie, List<MovieFileDto> Files);

/// <summary>Minimal movie file response.</summary>
/// <param name="Id">File id.</param>
/// <param name="SceneName">Original scene name.</param>
/// <param name="Edition">Edition of the file.</param>
/// <param name="MediaInfo">Technical media info.</param>
public sealed record MovieFileDto(int Id, string? SceneName, string? Edition, MediaInfoDto? MediaInfo);

/// <summary>Minimal media info response.</summary>
/// <param name="VideoCodec">Video codec.</param>
/// <param name="Width">Video width in pixels.</param>
/// <param name="Height">Video height in pixels.</param>
public sealed record MediaInfoDto(string? VideoCodec, int? Width, int? Height);

/// <summary>Minimal series folder response.</summary>
/// <param name="Folder">Folder path.</param>
/// <param name="Exists">Whether it exists.</param>
public sealed record SeriesFolderDto(string? Folder, bool Exists);

/// <summary>Minimal preview item response.</summary>
/// <param name="Status">New, exists, excluded or unresolved.</param>
public sealed record PreviewItemDto(string Status);

/// <summary>Minimal import list test response.</summary>
/// <param name="Success">Whether the fetch worked.</param>
/// <param name="ItemCount">Number of items.</param>
public sealed record ImportListTestResultDto(bool Success, int ItemCount);

/// <summary>Minimal schema response.</summary>
/// <param name="Type">Import list type.</param>
/// <param name="Fields">Field descriptors.</param>
public sealed record SchemaDto(string Type, List<FieldDto> Fields);

/// <summary>Minimal schema field response.</summary>
/// <param name="Name">Field name.</param>
public sealed record FieldDto(string Name);

/// <summary>Minimal PagedResult shape for tests.</summary>
/// <param name="Items">Page items.</param>
public sealed record PagedDto<T>(List<T> Items);

/// <summary>Returns JSON based on the requested custom import-list URL path.</summary>
public sealed class ImportListPathResponseHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
{
	/// <inheritdoc />
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var path = request.RequestUri!.AbsolutePath;
		return Task.FromResult(responses.TryGetValue(path, out var json)
			? new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
			}
			: new HttpResponseMessage(HttpStatusCode.NotFound));
	}
}
