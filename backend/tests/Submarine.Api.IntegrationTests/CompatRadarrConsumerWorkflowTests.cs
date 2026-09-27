using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Submarine.Api.IntegrationTests.Library;
using Submarine.Contracts.Metadata;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;
using Submarine.Core.Release.Torrent;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Xunit;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Replays sanitized request sequences from the researched Radarr-facing consumers against the real
///     pipeline, asserting native database/filesystem side effects rather than only HTTP response shape.
/// </summary>
public sealed class CompatRadarrConsumerWorkflowTests
{
	[Fact]
	public async Task OverseerrAddUpdateSearchWorkflow_ShouldAvoidDuplicateIdentityAndCompleteRealCommand()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var (qualityProfileId, rootPath) = await SeedCreationResourcesAsync(factory);
		const int tmdbId = 558216;
		factory.Metadata.Movies[tmdbId] = MovieResource(tmdbId, "Overseerr Requested Movie", "tt5582160");

		// Overseerr's registration flow reads profiles/roots/tags before ever adding anything.
		(await client.GetAsync("/compat/radarr/api/v3/qualityprofile")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync("/compat/radarr/api/v3/rootfolder")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync("/compat/radarr/api/v3/tag")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var initialLookup = await client.GetFromJsonAsync<JsonElement[]>($"/compat/radarr/api/v3/movie/lookup?term=tmdb:{tmdbId}");
		initialLookup.ShouldNotBeNull();
		initialLookup!.Length.ShouldBe(1);
		initialLookup[0].GetProperty("id").GetInt32().ShouldBe(0);

		var addResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/movie", new
		{
			tmdbId,
			title = "Overseerr Requested Movie",
			qualityProfileId,
			rootFolderPath = rootPath,
			monitored = true,
			addOptions = new { searchForMovie = false }
		});
		addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var added = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
		var movieId = added.GetProperty("id").GetInt32();

		// Duplicate lookup must hydrate the existing local id rather than a second metadata identity.
		var duplicateLookup = await client.GetFromJsonAsync<JsonElement[]>($"/compat/radarr/api/v3/movie/lookup?term=tmdb:{tmdbId}");
		duplicateLookup.ShouldNotBeNull();
		duplicateLookup!.Length.ShouldBe(1);
		duplicateLookup[0].GetProperty("id").GetInt32().ShouldBe(movieId);
		(await factory.WithDbAsync(db => db.Movies.CountAsync(x => x.TmdbId == tmdbId))).ShouldBe(1);

		// Seerr-style update: unmonitor plus a tag, real GET-modify-PUT round trip.
		var tagResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/tag", new { label = "overseerr" });
		var tagId = (await tagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
		var updateResponse = await client.PutAsJsonAsync($"/compat/radarr/api/v3/movie/{movieId}", new { id = movieId, monitored = false, tags = new[] { tagId } });
		updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
		updated.GetProperty("monitored").GetBoolean().ShouldBeFalse();
		(await factory.WithDbAsync(db => db.Movies.Where(x => x.Id == movieId).SelectMany(x => x.Tags).Select(x => x.Id).ToListAsync())).ShouldBe([tagId]);

		// Real MoviesSearch command must run through the actual command queue and complete.
		var commandResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/command", new { name = "MoviesSearch", movieIds = new[] { movieId } });
		commandResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var commandId = (await commandResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
		(await PollCommandStatusAsync(client, "radarr", commandId)).ShouldBe("completed");

		// Homepage/Overseerr queue read after a search with no releases must show an empty, well-formed page.
		var queue = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/queue");
		queue.GetProperty("totalRecords").GetInt32().ShouldBe(0);
	}

	[Fact]
	public async Task BazarrRealtimeAndSyncWorkflow_ShouldPublishRadarrShapedMovieEventsForTheSelectedVersionOnly()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var apiKey = client.DefaultRequestHeaders.GetValues("X-Api-Key").Single();
		var ids = await SeedMovieWithFileAsync(factory);

		// Bazarr's version/dialect probe: 4.x/6.x contract, never the native Submarine version.
		var status = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/system/status");
		status.GetProperty("version").GetString().ShouldStartWith("6.");

		var movie = (await client.GetFromJsonAsync<JsonElement[]>("/compat/radarr/api/v3/movie"))!.Single();
		movie.GetProperty("path").GetString().ShouldBe(Path.Combine(ids.RootPath, "Bazarr Movie"));
		movie.GetProperty("movieFile").GetProperty("path").GetString().ShouldBe(Path.Combine(ids.RootPath, "Bazarr Movie", "movie.mkv"));

		var updated = new List<JsonElement>();
		var deleted = new List<JsonElement>();
		await using var hub = new HubConnectionBuilder()
			.WithUrl($"{client.BaseAddress}compat/radarr/signalr/messages?access_token={apiKey}", options =>
				options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler())
			.Build();
		hub.On<JsonElement>("receiveMessage", envelope =>
		{
			if (envelope.GetProperty("name").GetString() != "movie")
			{
				return;
			}

			var body = envelope.GetProperty("body");
			(body.GetProperty("action").GetString() == "updated" ? updated : deleted).Add(envelope);
		});
		await hub.StartAsync(TestContext.Current.CancellationToken);

		// A native-shaped update (monitoring change) must reach Bazarr's realtime feed as a movie event.
		var putResponse = await client.PutAsJsonAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}", new { id = ids.MovieId, monitored = false });
		putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		await WaitForAsync(() => updated.Count >= 1, TimeSpan.FromSeconds(10));
		updated[0].GetProperty("body").GetProperty("resource").GetProperty("id").GetInt32().ShouldBe(ids.MovieId);

		// A selected-version file delete republishes the movie so Bazarr resyncs media info incrementally.
		var deleteFileResponse = await client.DeleteAsync($"/compat/radarr/api/v3/moviefile/{ids.MovieFileId}");
		deleteFileResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		await WaitForAsync(() => updated.Count >= 2, TimeSpan.FromSeconds(10));

		// A whole-title delete publishes a deletion carrying the identity Bazarr needs to drop its local row.
		var deleteResponse = await client.DeleteAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}?deleteFiles=false");
		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		await WaitForAsync(() => deleted.Count >= 1, TimeSpan.FromSeconds(10));
		deleted[0].GetProperty("body").GetProperty("resource").GetProperty("tmdbId").GetInt32().ShouldBe(ids.TmdbId);
	}

	[Fact]
	public async Task MaintainerrUnmonitorAndSelectedFileDeleteWorkflow_ShouldPreserveSiblingsAndRecordExclusion()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMultiVersionMovieAsync(factory);

		// Metadata-id filter read, then unmonitor.
		var byTmdb = await client.GetFromJsonAsync<JsonElement[]>($"/compat/radarr/api/v3/movie?tmdbId={ids.TmdbId}");
		byTmdb.ShouldNotBeNull();
		byTmdb!.Single().GetProperty("id").GetInt32().ShouldBe(ids.MovieId);
		var unmonitor = await client.PutAsJsonAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}", new { id = ids.MovieId, monitored = false });
		unmonitor.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await factory.WithDbAsync(db => db.Movies.Where(x => x.Id == ids.MovieId).Select(x => x.Monitored).SingleAsync())).ShouldBeFalse();

		// Selected-version file delete must not touch the sibling version's file.
		var deleteFile = await client.DeleteAsync($"/compat/radarr/api/v3/moviefile/{ids.SelectedFileId}");
		deleteFile.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await factory.WithDbAsync(db => db.MovieFiles.AnyAsync(x => x.Id == ids.SelectedFileId))).ShouldBeFalse();
		(await factory.WithDbAsync(db => db.MovieFiles.AnyAsync(x => x.Id == ids.SiblingFileId))).ShouldBeTrue();

		// Cleanup delete on the last-remaining-selected version records a real import exclusion.
		var delete = await client.DeleteAsync($"/compat/radarr/api/v3/movie/{ids.MovieId}?deleteFiles=false&addImportExclusion=true");
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var state = await factory.WithDbAsync(async db => new
		{
			MovieExists = await db.Movies.AnyAsync(x => x.Id == ids.MovieId),
			SiblingVersionExists = await db.MediaVersions.AnyAsync(x => x.Id == ids.SiblingVersionId),
			ExclusionExists = await db.ImportListExclusions.AnyAsync(x => x.TmdbId == ids.TmdbId)
		});
		state.MovieExists.ShouldBeTrue();
		state.SiblingVersionExists.ShouldBeTrue();
		state.ExclusionExists.ShouldBeTrue();
	}

	[Fact]
	public async Task KometaEditorBatchWorkflow_ShouldApplyTagsProfileAndVersionScopedMoveWithoutTouchingSiblingVersions()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();
		var ids = await SeedMultiVersionMovieAsync(factory);
		var newRootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-kometa-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(newRootPath);
		var newQualityProfileId = await factory.WithDbAsync(async db =>
		{
			var profile = new QualityProfile { Name = "Kometa profile" };
			db.QualityProfiles.Add(profile);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return profile.Id;
		});
		var newRootId = await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = newRootPath, MediaKind = MediaKind.MOVIES };
			db.RootFolders.Add(root);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return root.Id;
		});

		var tagResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/tag", new { label = "kometa" });
		var tagId = (await tagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

		var editorResponse = await client.PutAsJsonAsync("/compat/radarr/api/v3/movie/editor", new
		{
			movieIds = new[] { ids.MovieId },
			qualityProfileId = newQualityProfileId,
			rootFolderPath = newRootPath,
			moveFiles = true,
			tags = new[] { tagId },
			applyTags = "add"
		});
		editorResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

		await WaitForAsync(
			() => factory.WithDbAsync(db => db.MediaVersions.Where(x => x.Id == ids.SelectedVersionId).Select(x => x.RootFolderId).SingleAsync()).GetAwaiter().GetResult() == newRootId,
			TimeSpan.FromSeconds(15));

		var state = await factory.WithDbAsync(async db => new
		{
			SelectedVersion = await db.MediaVersions.Where(x => x.Id == ids.SelectedVersionId).Select(x => new { x.RootFolderId, x.QualityProfileId }).SingleAsync(),
			SiblingVersion = await db.MediaVersions.Where(x => x.Id == ids.SiblingVersionId).Select(x => new { x.RootFolderId, x.QualityProfileId }).SingleAsync(),
			Tags = await db.Movies.Where(x => x.Id == ids.MovieId).SelectMany(x => x.Tags).Select(x => x.Id).ToListAsync()
		});
		state.SelectedVersion.RootFolderId.ShouldBe(newRootId);
		state.SelectedVersion.QualityProfileId.ShouldBe(newQualityProfileId);
		state.SiblingVersion.RootFolderId.ShouldBe(ids.RootId);
		state.SiblingVersion.QualityProfileId.ShouldNotBe(newQualityProfileId);
		state.Tags.ShouldContain(tagId);
		Directory.Exists(Path.Combine(newRootPath, "Kometa Movie")).ShouldBeTrue();
	}

	[Fact]
	public async Task RecyclarrRadarrSyncWorkflow_ShouldRoundTripConfigWithNoDriftOnSecondSync()
	{
		await using var factory = new LibraryApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();

		// Custom format sync: create, then a second identical PUT must not change anything further.
		var createFormat = await client.PostAsJsonAsync("/compat/radarr/api/v3/customformat", new
		{
			name = "Recyclarr Format",
			includeCustomFormatWhenRenaming = false,
			specifications = new[]
			{
				new { name = "Release Title", implementation = "ReleaseTitleSpecification", negate = false, required = true, fields = new[] { new { name = "value", value = "PROPER" } } }
			}
		});
		createFormat.StatusCode.ShouldBe(HttpStatusCode.Created);
		var format = await createFormat.Content.ReadFromJsonAsync<JsonElement>();
		var formatId = format.GetProperty("id").GetInt32();

		var syncFormat = await client.PutAsJsonAsync($"/compat/radarr/api/v3/customformat/{formatId}", format);
		syncFormat.StatusCode.ShouldBe(HttpStatusCode.OK);
		var secondSyncFormat = await client.PutAsJsonAsync($"/compat/radarr/api/v3/customformat/{formatId}", format);
		secondSyncFormat.StatusCode.ShouldBe(HttpStatusCode.OK);
		var afterSecondFormat = await client.GetFromJsonAsync<JsonElement>($"/compat/radarr/api/v3/customformat/{formatId}");
		afterSecondFormat.GetProperty("name").GetString().ShouldBe("Recyclarr Format");
		afterSecondFormat.GetProperty("specifications").GetArrayLength().ShouldBe(1);

		// Quality profile sync from schema, referencing the created format, twice with identical bodies.
		var schema = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/qualityprofile/schema");
		// Recyclarr profiles specify an explicit, applicable quality subset rather than the full native enum.
		// BR-DISK (id 22) covers three native resolutions under one upstream id, so it round-trips ambiguously
		// and is excluded here exactly like the write endpoint excludes it; every other id is unique and writable.
		var applicableItems = schema.GetProperty("items").EnumerateArray()
			.Where(item => item.GetProperty("quality").TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.Number)
			.GroupBy(item => item.GetProperty("quality").GetProperty("id").GetInt32())
			.Where(group => group.Count() == 1)
			.Select(group => group.Single())
			.ToArray();
		var profileBody = JsonSerializer.SerializeToElement(new
		{
			name = "Recyclarr Profile",
			upgradeAllowed = true,
			cutoff = applicableItems[0].GetProperty("quality").GetProperty("id").GetInt32(),
			items = applicableItems,
			formatItems = new[] { new { format = new { id = formatId }, score = 25 } },
			minFormatScore = 0,
			cutoffFormatScore = 100,
			minUpgradeFormatScore = 1
		});
		var createProfile = await client.PostAsync("/compat/radarr/api/v3/qualityprofile", JsonContent(profileBody));
		createProfile.StatusCode.ShouldBe(HttpStatusCode.Created);
		var profile = await createProfile.Content.ReadFromJsonAsync<JsonElement>();
		var profileId = profile.GetProperty("id").GetInt32();
		var profileWithId = JsonSerializer.SerializeToElement(new
		{
			id = profileId,
			name = "Recyclarr Profile",
			upgradeAllowed = true,
			cutoff = profile.GetProperty("cutoff").GetInt32(),
			items = profile.GetProperty("items"),
			formatItems = profile.GetProperty("formatItems"),
			minFormatScore = 0,
			cutoffFormatScore = 100,
			minUpgradeFormatScore = 1
		});
		var firstSync = await client.PutAsync($"/compat/radarr/api/v3/qualityprofile/{profileId}", JsonContent(profileWithId));
		firstSync.StatusCode.ShouldBe(HttpStatusCode.OK);
		var afterFirstSync = await firstSync.Content.ReadFromJsonAsync<JsonElement>();
		var secondSync = await client.PutAsync($"/compat/radarr/api/v3/qualityprofile/{profileId}", JsonContent(profileWithId));
		secondSync.StatusCode.ShouldBe(HttpStatusCode.OK);
		var afterSecondSync = await secondSync.Content.ReadFromJsonAsync<JsonElement>();
		afterSecondSync.GetProperty("cutoff").GetInt32().ShouldBe(afterFirstSync.GetProperty("cutoff").GetInt32());
		afterSecondSync.GetProperty("formatItems")[0].GetProperty("score").GetInt32().ShouldBe(25);

		// Quality definition batch sync twice with identical values must leave the row unchanged. The compat
		// "id" is the upstream quality id (e.g. WEBDL-1080p = 3), not the native QualityDefinitions row id.
		var definitions = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/qualitydefinition");
		var target = definitions.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == 3);
		var definitionUpdate = new[]
		{
			(object)new { id = 3, minSize = 10d, maxSize = 200d, preferredSize = 100d, title = target.GetProperty("title").GetString(), weight = target.GetProperty("weight").GetInt32() }
		};
		(await client.PutAsJsonAsync("/compat/radarr/api/v3/qualitydefinition/update", definitionUpdate)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.PutAsJsonAsync("/compat/radarr/api/v3/qualitydefinition/update", definitionUpdate)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var afterDefinitionSync = await factory.WithDbAsync(db => db.QualityDefinitions
			.Where(x => x.Source == Submarine.Core.Quality.QualitySource.WEB_DL && x.Resolution == Submarine.Core.Quality.QualityResolution.R1080_P)
			.SingleAsync());
		afterDefinitionSync.MinSizeMbPerMinute.ShouldBe(10d);
		afterDefinitionSync.MaxSizeMbPerMinute.ShouldBe(200d);

		// Naming config: read, write the same movie format twice, confirm no drift.
		var naming = await client.GetFromJsonAsync<JsonElement>("/compat/radarr/api/v3/config/naming");
		var namingBody = JsonSerializer.SerializeToElement(new { id = 1, renameMovies = true, standardMovieFormat = "{Movie Title} ({Release Year})", movieFolderFormat = "{Movie Title} ({Release Year})" });
		(await client.PutAsync("/compat/radarr/api/v3/config/naming/1", JsonContent(namingBody))).StatusCode.ShouldBe(HttpStatusCode.OK);
		var afterNamingFirst = await client.PutAsync("/compat/radarr/api/v3/config/naming/1", JsonContent(namingBody));
		afterNamingFirst.StatusCode.ShouldBe(HttpStatusCode.OK);
		var namingResult = await afterNamingFirst.Content.ReadFromJsonAsync<JsonElement>();
		namingResult.GetProperty("standardMovieFormat").GetString().ShouldBe("{Movie Title} ({Release Year})");
	}

	[Fact]
	public async Task PostRelease_ShouldGrabFromAnyRecentSearchWithoutRequiringMovieIdInTheRequest()
	{
		await using var factory = new RadarrReleaseGrabApiFactory();
		var client = await factory.CreateAuthorizedClientAsync();

		var downloadClient = Substitute.For<IDownloadClient>();
		downloadClient.Protocol.Returns(Protocol.BITTORRENT);
		downloadClient.AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>()).Returns("LUNASEA-GRAB");
		factory.DownloadClientFactory.Create(Arg.Any<DownloadClientType>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(downloadClient);

		var (movieId, versionId) = await factory.WithDbAsync(async db =>
		{
			var quality = new QualityProfile
			{
				Name = "Release grab quality",
				UpgradeAllowed = true,
				Cutoff = 0,
				Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)]
			};
			var language = new LanguageProfile { Name = "Release grab language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var root = new RootFolder { Path = "/it-root-release-grab", MediaKind = MediaKind.MOVIES };
			db.AddRange(quality, language, root);
			var movie = new Movie { TmdbId = 778812, Title = "Release Grab Movie", SortTitle = "release grab movie", CleanTitle = "releasegrabmovie", Year = 2024 };
			db.Movies.Add(movie);
			await db.SaveChangesAsync();
			var version = new MediaVersion { MovieId = movie.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "release-grab" };
			db.MediaVersions.Add(version);
			db.DownloadClients.Add(new DownloadClient { Name = "Client", Type = DownloadClientType.QBITTORRENT, Enable = true });
			db.Indexers.Add(new Indexer { Id = 909, Name = "Stub", BaseUrl = "http://x" });
			await db.SaveChangesAsync();
			return (movie.Id, version.Id);
		});

		// Bind the facade to the created version, matching what a prior facade access would have done.
		(await client.GetFromJsonAsync<JsonElement>($"/compat/radarr/api/v3/movie/{movieId}")).GetProperty("id").GetInt32().ShouldBe(movieId);

		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = "Release.Grab.Movie.2024.1080p.WEB-DL-GROUP",
			Title = "Release Grab Movie",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var info = new ReleaseInfo
		{
			Guid = "lunasea-cached-guid",
			Title = release.FullTitle,
			MagnetUrl = "magnet:?xt=urn:btih:release-grab",
			Size = 555555,
			Protocol = Protocol.BITTORRENT,
			IndexerId = 909,
			Indexer = "Stub"
		};
		var cache = factory.Services.GetRequiredService<ReleaseResultCache>();
		// MatchedMovieId is set exactly as it would be after a real movie-scoped search, without the client
		// ever having to echo a movieId back on grab, matching upstream Radarr's bare {guid,indexerId} grab.
		cache.Store(new ReleaseCandidate(release, info, null, null, movieId, null));

		var grabResponse = await client.PostAsJsonAsync("/compat/radarr/api/v3/release", new { guid = "lunasea-cached-guid", indexerId = 909 });
		grabResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var grabbed = await grabResponse.Content.ReadFromJsonAsync<JsonElement>();
		grabbed.GetProperty("grabbed").GetBoolean().ShouldBeTrue();

		var tracked = await factory.WithDbAsync(db => db.TrackedDownloads.SingleAsync(x => x.MediaVersionId == versionId));
		tracked.DownloadId.ShouldBe("LUNASEA-GRAB");
		tracked.MovieId.ShouldBe(movieId);
	}

	private static async Task<(int QualityProfileId, string RootPath)> SeedCreationResourcesAsync(LibraryApiFactory factory)
	{
		var rootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-workflow-root-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(rootPath);
		return await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = rootPath, MediaKind = MediaKind.MOVIES };
			var quality = new QualityProfile { Name = "Workflow add quality" };
			var language = new LanguageProfile { Name = "Workflow add language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.AddRange(root, quality, language);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (quality.Id, rootPath);
		});
	}

	private static async Task<(int MovieId, int TmdbId, int MovieFileId, string RootPath)> SeedMovieWithFileAsync(LibraryApiFactory factory)
	{
		var rootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-workflow-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(rootPath, "Bazarr Movie"));
		return await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = rootPath, MediaKind = MediaKind.MOVIES };
			var quality = new QualityProfile { Name = "Bazarr sync quality" };
			var language = new LanguageProfile { Name = "Bazarr sync language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var movie = new Movie { TmdbId = 245891, ImdbId = "tt2458910", Title = "Bazarr Movie", SortTitle = "bazarr movie", CleanTitle = "bazarrmovie", Year = 2024 };
			db.AddRange(root, quality, language, movie);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var version = new MediaVersion { MovieId = movie.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Bazarr Movie" };
			db.MediaVersions.Add(version);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var file = new MovieFile { MovieId = movie.Id, MediaVersionId = version.Id, RelativePath = "movie.mkv", DateAdded = DateTime.UtcNow, Size = 1000 };
			db.MovieFiles.Add(file);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return (movie.Id, movie.TmdbId, file.Id, rootPath);
		});
	}

	private static async Task<MultiVersionIds> SeedMultiVersionMovieAsync(LibraryApiFactory factory)
	{
		var rootPath = Path.Combine(Path.GetTempPath(), "radarr-compat-workflow-multi-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(rootPath, "Kometa Movie"));
		return await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = rootPath, MediaKind = MediaKind.MOVIES };
			var quality = new QualityProfile { Name = "Multi version quality" };
			var language = new LanguageProfile { Name = "Multi version language", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var movie = new Movie { TmdbId = 991234, ImdbId = "tt9912340", Title = "Kometa Movie", SortTitle = "kometa movie", CleanTitle = "kometamovie", Year = 2024 };
			db.AddRange(root, quality, language, movie);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var selected = new MediaVersion { MovieId = movie.Id, Name = "main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Kometa Movie" };
			var sibling = new MediaVersion { MovieId = movie.Id, Name = "sibling", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Kometa Movie Sibling" };
			db.MediaVersions.AddRange(selected, sibling);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			var selectedFile = new MovieFile { MovieId = movie.Id, MediaVersionId = selected.Id, RelativePath = "selected.mkv", DateAdded = DateTime.UtcNow, Size = 1000 };
			var siblingFile = new MovieFile { MovieId = movie.Id, MediaVersionId = sibling.Id, RelativePath = "sibling.mkv", DateAdded = DateTime.UtcNow, Size = 1000 };
			db.MovieFiles.AddRange(selectedFile, siblingFile);
			await db.SaveChangesAsync(TestContext.Current.CancellationToken);
			return new MultiVersionIds(movie.Id, movie.TmdbId, root.Id, selected.Id, sibling.Id, selectedFile.Id, siblingFile.Id);
		});
	}

	private static MovieResource MovieResource(int tmdbId, string title, string imdbId)
		=> new(tmdbId, imdbId, title, title, title, "A test movie", new DateOnly(2024, 1, 1), null, null,
			Submarine.Contracts.Metadata.MovieStatus.RELEASED, 2024, 110, ["Drama"], "Test Studio", "PG", null, null, null, null, null, []);

	private static HttpContent JsonContent(JsonElement value)
		=> new StringContent(value.GetRawText(), System.Text.Encoding.UTF8, "application/json");

	private static async Task<string> PollCommandStatusAsync(HttpClient client, string facade, int id)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			var response = await client.GetFromJsonAsync<JsonElement>($"/compat/{facade}/api/v3/command/{id}");
			var status = response.GetProperty("status").GetString()!;
			if (status is "completed" or "failed" or "aborted")
			{
				return status;
			}

			await Task.Delay(200);
		}

		throw new TimeoutException($"Command {id} did not finish in time");
	}

	private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow.Add(timeout);
		while (DateTime.UtcNow < deadline)
		{
			if (predicate())
			{
				return;
			}

			await Task.Delay(100);
		}

		throw new TimeoutException("Condition was not met in time.");
	}

	private sealed record MultiVersionIds(int MovieId, int TmdbId, int RootId, int SelectedVersionId, int SiblingVersionId, int SelectedFileId, int SiblingFileId);

	private sealed class RadarrReleaseGrabApiFactory : SubmarineApiFactory
	{
		public IDownloadClientFactory DownloadClientFactory { get; } = Substitute.For<IDownloadClientFactory>();

		protected override void ConfigureTestServices(IServiceCollection services)
		{
			services.RemoveAll<IDownloadClientFactory>();
			services.AddSingleton(DownloadClientFactory);
		}
	}
}
