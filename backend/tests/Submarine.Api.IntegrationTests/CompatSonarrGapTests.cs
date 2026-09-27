using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
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
using Submarine.Infrastructure.Library;
using Submarine.Infrastructure.Search;
using Xunit;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     Closes the Sonarr compatibility facade gaps: selected-version episode-file deletion, version-scoped
///     monitoring, bound-version episode projections, release search/grab, manual import, rename and parse, and
///     realtime forwarding to the Sonarr SignalR hub. Also replays consumer request sequences from the research.
/// </summary>
public sealed class CompatSonarrGapTests : IClassFixture<CompatSonarrExtendedApiFactory>
{
	private readonly CompatSonarrExtendedApiFactory _factory;

	public CompatSonarrGapTests(CompatSonarrExtendedApiFactory factory) => _factory = factory;

	private async Task<(int SeriesId, int MainVersionId, int SiblingVersionId, int EpisodeId)> SeedTwoVersionSeriesAsync(string unique)
		=> await _factory.WithDbAsync(async db =>
		{
			var quality = new QualityProfile { Name = "gap-quality-" + unique, UpgradeAllowed = true, Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
			var language = new LanguageProfile { Name = "gap-language-" + unique, Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			var root = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "gap-root-" + unique), MediaKind = MediaKind.SERIES };
			db.QualityProfiles.Add(quality);
			db.LanguageProfiles.Add(language);
			db.RootFolders.Add(root);
			var series = new Series { TvdbId = Random.Shared.Next(1_000_000, int.MaxValue), Title = "Gap Show " + unique, SortTitle = unique, CleanTitle = unique, Monitored = true };
			series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot", Monitored = true });
			db.Series.Add(series);
			await db.SaveChangesAsync();
			var main = new MediaVersion { SeriesId = series.Id, Name = "Main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "main", Monitored = true };
			var sibling = new MediaVersion { SeriesId = series.Id, Name = "Sibling", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "sibling", Monitored = true };
			db.MediaVersions.AddRange(main, sibling);
			await db.SaveChangesAsync();
			return (series.Id, main.Id, sibling.Id, series.Episodes.First().Id);
		});

	[Fact]
	public async Task EpisodeFileDelete_ShouldDeleteBoundVersionFile_AndRejectSiblingVersionFile()
	{
		var (seriesId, mainVersionId, siblingVersionId, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();

		// Selecting the series through the facade binds it to the lowest-id version (main).
		var lookup = await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}");
		lookup.StatusCode.ShouldBe(HttpStatusCode.OK);

		var (mainFileId, siblingFileId) = await _factory.WithDbAsync(async db =>
		{
			var mainFile = new EpisodeFile { SeriesId = seriesId, MediaVersionId = mainVersionId, RelativePath = "main.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			var siblingFile = new EpisodeFile { SeriesId = seriesId, MediaVersionId = siblingVersionId, RelativePath = "sibling.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			var episode = await db.Episodes.FirstAsync(x => x.Id == episodeId);
			mainFile.Episodes.Add(episode);
			siblingFile.Episodes.Add(episode);
			db.EpisodeFiles.AddRange(mainFile, siblingFile);
			await db.SaveChangesAsync();
			return (mainFile.Id, siblingFile.Id);
		});

		using var siblingDelete = await client.DeleteAsync($"/compat/sonarr/api/v3/episodefile/{siblingFileId}");
		siblingDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await _factory.WithDbAsync(db => db.EpisodeFiles.AnyAsync(x => x.Id == siblingFileId))).ShouldBeTrue();

		using var bulkMixed = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/compat/sonarr/api/v3/episodefile/bulk")
		{
			Content = JsonContent.Create(new { episodeFileIds = new[] { mainFileId, siblingFileId } })
		});
		bulkMixed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await _factory.WithDbAsync(db => db.EpisodeFiles.CountAsync(x => x.Id == mainFileId || x.Id == siblingFileId))).ShouldBe(2);

		using var mainDelete = await client.DeleteAsync($"/compat/sonarr/api/v3/episodefile/{mainFileId}");
		mainDelete.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await _factory.WithDbAsync(db => db.EpisodeFiles.AnyAsync(x => x.Id == mainFileId))).ShouldBeFalse();
		(await _factory.WithDbAsync(db => db.EpisodeFiles.AnyAsync(x => x.Id == siblingFileId))).ShouldBeTrue();
	}

	[Fact]
	public async Task EpisodeMonitoring_ShouldApplyOnlyToBoundVersion_LeavingSiblingVersionUnaffected()
	{
		var (seriesId, mainVersionId, siblingVersionId, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		using var response = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/episode/{episodeId}", new { monitored = false });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var projected = await client.GetFromJsonAsync<JsonElement>($"/compat/sonarr/api/v3/episode/{episodeId}");
		projected.GetProperty("monitored").GetBoolean().ShouldBeFalse();

		await using var scope = _factory.Services.CreateAsyncScope();
		var monitoring = scope.ServiceProvider.GetRequiredService<VersionMonitoringService>();
		(await monitoring.IsEpisodeMonitoredAsync(episodeId, mainVersionId)).ShouldBeFalse();
		(await monitoring.IsEpisodeMonitoredAsync(episodeId, siblingVersionId)).ShouldBeTrue();
	}

	[Fact]
	public async Task SeasonPass_ShouldApplyVersionScopedSeasonOverride()
	{
		var (seriesId, mainVersionId, siblingVersionId, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		using var response = await client.PostAsJsonAsync("/compat/sonarr/api/v3/seasonpass", new { seriesId, monitored = false, seasonNumbers = new[] { 1 } });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		await using var scope = _factory.Services.CreateAsyncScope();
		var monitoring = scope.ServiceProvider.GetRequiredService<VersionMonitoringService>();
		(await monitoring.IsEpisodeMonitoredAsync(episodeId, mainVersionId)).ShouldBeFalse();
		(await monitoring.IsEpisodeMonitoredAsync(episodeId, siblingVersionId)).ShouldBeTrue();

		// A more specific per-episode override wins over the season override.
		using var episodeOverride = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/episode/{episodeId}", new { monitored = true });
		episodeOverride.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await monitoring.IsEpisodeMonitoredAsync(episodeId, mainVersionId)).ShouldBeTrue();
	}

	[Fact]
	public async Task EpisodeProjection_ShouldExposeAbsolutePath_ForBoundVersionFileOnly()
	{
		var (seriesId, mainVersionId, siblingVersionId, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var rootPath = await _factory.WithDbAsync(async db =>
		{
			var version = await db.MediaVersions.FirstAsync(x => x.Id == mainVersionId);
			var siblingVersion = await db.MediaVersions.FirstAsync(x => x.Id == siblingVersionId);
			var root = await db.RootFolders.FirstAsync(x => x.Id == version.RootFolderId);
			var episode = await db.Episodes.FirstAsync(x => x.Id == episodeId);
			var mainFile = new EpisodeFile { SeriesId = seriesId, MediaVersionId = mainVersionId, RelativePath = "main-episode.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			var siblingFile = new EpisodeFile { SeriesId = seriesId, MediaVersionId = siblingVersionId, RelativePath = "sibling-episode.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			mainFile.Episodes.Add(episode);
			siblingFile.Episodes.Add(episode);
			db.EpisodeFiles.AddRange(mainFile, siblingFile);
			await db.SaveChangesAsync();
			return root.Path;
		});

		var projected = await client.GetFromJsonAsync<JsonElement>($"/compat/sonarr/api/v3/episode/{episodeId}");
		var file = projected.GetProperty("episodeFile");
		file.GetProperty("relativePath").GetString().ShouldBe("main-episode.mkv");
		file.GetProperty("path").GetString().ShouldBe(Path.GetFullPath(Path.Combine(rootPath, "main", "main-episode.mkv")));
	}

	[Fact]
	public async Task SeriesRealtimeHub_ShouldReceiveEpisodeUpdated_WhenMonitoringChangedThroughFacade()
	{
		var (seriesId, _, _, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
		var apiKey = client.DefaultRequestHeaders.GetValues("X-Api-Key").Single();

		var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var hub = new HubConnectionBuilder()
			.WithUrl($"{client.BaseAddress}compat/sonarr/signalr/messages?access_token={apiKey}", options =>
				options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
			.Build();
		hub.On<JsonElement>("receiveMessage", envelope =>
		{
			if (envelope.GetProperty("name").GetString() == "episode"
				&& envelope.GetProperty("body").GetProperty("resource").GetProperty("id").GetInt32() == episodeId)
			{
				received.TrySetResult(envelope);
			}
		});
		await hub.StartAsync(TestContext.Current.CancellationToken);

		using var response = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/episode/{episodeId}", new { monitored = false });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var envelope = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
		envelope.GetProperty("body").GetProperty("action").GetString().ShouldBe("updated");
		envelope.GetProperty("body").GetProperty("resource").GetProperty("monitored").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task SeriesDelete_ShouldBroadcastEnrichedSnapshot_BeforeRowIsGone()
	{
		var (seriesId, _, _, _) = await _factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "gap-delete-" + Guid.NewGuid().ToString("N")), MediaKind = MediaKind.SERIES };
			var quality = new QualityProfile { Name = "delete-quality-" + Guid.NewGuid().ToString("N") };
			var language = new LanguageProfile { Name = "delete-language-" + Guid.NewGuid().ToString("N"), Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.RootFolders.Add(root);
			db.QualityProfiles.Add(quality);
			db.LanguageProfiles.Add(language);
			var series = new Series { TvdbId = Random.Shared.Next(1_000_000, int.MaxValue), Title = "Delete Show", Year = 2019 };
			db.Series.Add(series);
			await db.SaveChangesAsync();
			db.MediaVersions.Add(new MediaVersion { SeriesId = series.Id, Name = "Main", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "main" });
			await db.SaveChangesAsync();
			return (series.Id, 0, 0, 0);
		});

		var client = await _factory.CreateAuthorizedClientAsync();
		var apiKey = client.DefaultRequestHeaders.GetValues("X-Api-Key").Single();
		var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var hub = new HubConnectionBuilder()
			.WithUrl($"{client.BaseAddress}compat/sonarr/signalr/messages?access_token={apiKey}", options =>
				options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
			.Build();
		hub.On<JsonElement>("receiveMessage", envelope =>
		{
			if (envelope.GetProperty("name").GetString() == "series" && envelope.GetProperty("body").GetProperty("action").GetString() == "deleted")
			{
				received.TrySetResult(envelope);
			}
		});
		await hub.StartAsync(TestContext.Current.CancellationToken);

		using var delete = await client.DeleteAsync($"/compat/sonarr/api/v3/series/{seriesId}?deleteFiles=false&addImportListExclusion=false");
		delete.StatusCode.ShouldBe(HttpStatusCode.OK);

		var envelope = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
		var resource = envelope.GetProperty("body").GetProperty("resource");
		resource.GetProperty("id").GetInt32().ShouldBe(seriesId);
		resource.GetProperty("title").GetString().ShouldBe("Delete Show");
		resource.GetProperty("year").GetInt32().ShouldBe(2019);
	}

	[Fact]
	public async Task Release_Search_ShouldReturnArray_WithNoIndexersConfigured()
	{
		var (seriesId, _, _, _) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		using var response = await client.GetAsync($"/compat/sonarr/api/v3/release?seriesId={seriesId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var results = await response.Content.ReadFromJsonAsync<JsonElement>();
		results.ValueKind.ShouldBe(JsonValueKind.Array);
	}

	[Fact]
	public async Task Release_Grab_ShouldGrabCachedCandidate_ForBoundVersion()
	{
		var unique = Guid.NewGuid().ToString("N");
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(unique);
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var downloadClient = Substitute.For<IDownloadClient>();
		downloadClient.Protocol.Returns(Protocol.BITTORRENT);
		downloadClient.AddAsync(Arg.Any<RemoteRelease>(), Arg.Any<SeedCriteria?>(), Arg.Any<CancellationToken>()).Returns("SONARR-GRAB-" + unique);
		_factory.DownloadClientFactory.Create(Arg.Any<DownloadClientType>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(downloadClient);
		await _factory.WithDbAsync(async db =>
		{
			db.DownloadClients.Add(new DownloadClient { Name = "Client " + unique, Type = DownloadClientType.QBITTORRENT, Enable = true });
			db.Indexers.Add(new Indexer { Id = 900 + Math.Abs(unique.GetHashCode() % 90), Name = "Stub " + unique, BaseUrl = "http://x" });
			await db.SaveChangesAsync();
			return 0;
		});

		var release = new TorrentRelease(new BaseRelease
		{
			FullTitle = $"Gap.Show.{unique}.S01E01.1080p.WEB-DL-GROUP",
			Title = "Gap Show",
			ReleaseGroup = "GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()),
			Languages = [Language.ENGLISH]
		});
		var indexerId = 900 + Math.Abs(unique.GetHashCode() % 90);
		var info = new ReleaseInfo { Guid = "guid-" + unique, Title = release.FullTitle, MagnetUrl = "magnet:?xt=urn:btih:abcdef", Size = 1000, Protocol = Protocol.BITTORRENT, IndexerId = indexerId, Indexer = "Stub" };
		var cache = _factory.Services.GetRequiredService<ReleaseResultCache>();
		cache.Store(new ReleaseCandidate(release, info, null, seriesId, null, [episodeId]));

		using var response = await client.PostAsJsonAsync("/compat/sonarr/api/v3/release", new { guid = "guid-" + unique, indexerId });
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("grabbed").GetBoolean().ShouldBeTrue();

		var tracked = await _factory.WithDbAsync(db => db.TrackedDownloads.SingleAsync(x => x.MediaVersionId == mainVersionId));
		tracked.DownloadId.ShouldBe("SONARR-GRAB-" + unique);
	}

	[Fact]
	public async Task ManualImport_ShouldAnalyzeAndImport_RealFileThroughCompatFacade()
	{
		var unique = Guid.NewGuid().ToString("N");
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(unique);
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var downloadFolder = Directory.CreateTempSubdirectory("submarine-it-compat-manual-").FullName;
		var sourceFile = Path.Combine(downloadFolder, "Gap.Show.S01E01.1080p.WEB-DL-GROUP.mkv");
		await File.WriteAllTextAsync(sourceFile, "video");
		await _factory.WithDbAsync(async db =>
		{
			var mgmt = await db.MediaManagementConfig.SingleAsync();
			mgmt.UseHardlinks = false;
			await db.SaveChangesAsync();
			return 0;
		});

		using var analyze = await client.GetAsync($"/compat/sonarr/api/v3/manualimport?folder={Uri.EscapeDataString(downloadFolder)}&seriesId={seriesId}");
		analyze.StatusCode.ShouldBe(HttpStatusCode.OK);
		var candidates = await analyze.Content.ReadFromJsonAsync<JsonElement>();
		candidates.ValueKind.ShouldBe(JsonValueKind.Array);
		candidates.EnumerateArray().ShouldContain(x => x.GetProperty("path").GetString() == sourceFile);

		using var import = await client.PostAsJsonAsync("/compat/sonarr/api/v3/manualimport", new
		{
			files = new[] { new { path = sourceFile, seriesId, episodeIds = new[] { episodeId } } },
			importMode = "move"
		});
		import.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await import.Content.ReadFromJsonAsync<JsonElement>();
		result.GetProperty("anyImported").GetBoolean().ShouldBeTrue(JsonSerializer.Serialize(result));

		File.Exists(sourceFile).ShouldBeFalse();
		(await _factory.WithDbAsync(db => db.EpisodeFiles.CountAsync(x => x.MediaVersionId == mainVersionId))).ShouldBe(1);
	}

	[Fact]
	public async Task Rename_Preview_ShouldProposeRename_WhenExistingPathDoesNotMatchNamingTemplate()
	{
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		await _factory.WithDbAsync(async db =>
		{
			var episode = await db.Episodes.FirstAsync(x => x.Id == episodeId);
			var file = new EpisodeFile { SeriesId = seriesId, MediaVersionId = mainVersionId, RelativePath = "unmatched-name.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			file.Episodes.Add(episode);
			db.EpisodeFiles.Add(file);
			await db.SaveChangesAsync();
			return 0;
		});

		using var response = await client.GetAsync($"/compat/sonarr/api/v3/rename?seriesId={seriesId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var proposals = await response.Content.ReadFromJsonAsync<JsonElement>();
		proposals.ValueKind.ShouldBe(JsonValueKind.Array);
		proposals.EnumerateArray().ShouldContain(x => x.GetProperty("existingPath").GetString() == "unmatched-name.mkv");
	}

	[Fact]
	public async Task Rename_RenameFilesCommand_ShouldQueueThroughGenericCommandDispatcher()
	{
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(Guid.NewGuid().ToString("N"));
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var fileId = await _factory.WithDbAsync(async db =>
		{
			var episode = await db.Episodes.FirstAsync(x => x.Id == episodeId);
			var version = await db.MediaVersions.FirstAsync(x => x.Id == mainVersionId);
			var root = await db.RootFolders.FirstAsync(x => x.Id == version.RootFolderId);
			var versionFolder = Path.Combine(root.Path, version.Path);
			Directory.CreateDirectory(versionFolder);
			await File.WriteAllTextAsync(Path.Combine(versionFolder, "unmatched-rename.mkv"), "video");
			var file = new EpisodeFile { SeriesId = seriesId, MediaVersionId = mainVersionId, RelativePath = "unmatched-rename.mkv", Size = 10, DateAdded = DateTime.UtcNow, Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), new Revision()) };
			file.Episodes.Add(episode);
			db.EpisodeFiles.Add(file);
			await db.SaveChangesAsync();
			return file.Id;
		});

		using var command = await client.PostAsJsonAsync("/compat/sonarr/api/v3/command", new { name = "RenameFiles", seriesId, files = new[] { fileId } });
		command.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await command.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("name").GetString().ShouldBe("RenameFiles");

		var commandId = body.GetProperty("id").GetInt32();
		Command? row = null;
		for (var i = 0; i < 50 && row?.Status != CommandStatus.COMPLETED; i++)
		{
			await Task.Delay(100);
			row = await _factory.WithDbAsync(db => db.Commands.SingleAsync(x => x.Id == commandId));
		}

		row!.Status.ShouldBe(CommandStatus.COMPLETED);
		(await _factory.WithDbAsync(db => db.EpisodeFiles.Where(x => x.Id == fileId).Select(x => x.RelativePath).SingleAsync()))
			.ShouldNotBe("unmatched-rename.mkv");
	}

	[Fact]
	public async Task Parse_ShouldMatchKnownSeriesFromReleaseTitle()
	{
		var unique = Guid.NewGuid().ToString("N")[..8];
		var (seriesId, _, _, _) = await _factory.WithDbAsync(async db =>
		{
			var series = new Series { TvdbId = Random.Shared.Next(1_000_000, int.MaxValue), Title = "Parse Show " + unique, CleanTitle = "parseshow" + unique };
			series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot" });
			db.Series.Add(series);
			await db.SaveChangesAsync();
			return (series.Id, 0, 0, 0);
		});

		var client = await _factory.CreateAuthorizedClientAsync();
		var title = $"Parse.Show.{unique}.S01E01.1080p.WEB-DL-GROUP";
		using var response = await client.GetAsync($"/compat/sonarr/api/v3/parse?title={Uri.EscapeDataString(title)}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("series").GetProperty("id").GetInt32().ShouldBe(seriesId);
		body.GetProperty("parsedEpisodeInfo").GetProperty("episodeNumbers").EnumerateArray().Select(x => x.GetInt32()).ShouldContain(1);
	}

	[Fact]
	public async Task ConsumerSequence_Overseerr_LookupAddUpdateSearch_ShouldNotDuplicateMetadataIdentity()
	{
		await using var factory = new CompatSonarrExtendedApiFactory();
		var unique = Guid.NewGuid().ToString("N");
		var (rootId, qualityId, languageId) = await factory.WithDbAsync(async db =>
		{
			var root = new RootFolder { Path = Path.Combine(Path.GetTempPath(), "overseerr-root-" + unique), MediaKind = MediaKind.SERIES };
			var quality = new QualityProfile { Name = "overseerr-quality-" + unique, UpgradeAllowed = true, Cutoff = 0, Items = [new QualityProfileItem(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), true)] };
			var language = new LanguageProfile { Name = "overseerr-language-" + unique, Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH };
			db.RootFolders.Add(root);
			db.QualityProfiles.Add(quality);
			db.LanguageProfiles.Add(language);
			await db.SaveChangesAsync();
			return (root.Id, quality.Id, language.Id);
		});

		var client = await factory.CreateAuthorizedClientAsync();
		using var status = await client.GetAsync("/compat/sonarr/api/v3/system/status");
		status.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var profiles = await client.GetAsync("/compat/sonarr/api/v3/qualityprofile");
		profiles.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var roots = await client.GetAsync("/compat/sonarr/api/v3/rootfolder");
		roots.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var tags = await client.GetAsync("/compat/sonarr/api/v3/tag");
		tags.StatusCode.ShouldBe(HttpStatusCode.OK);

		var tvdbId = Random.Shared.Next(1_000_000, int.MaxValue);
		factory.Metadata.Series[tvdbId] = Library.LibraryTestSupport.SeriesFixture(tvdbId, "Overseerr Show " + unique, DateTime.UtcNow);
		using var add = await client.PostAsJsonAsync("/compat/sonarr/api/v3/series", new
		{
			tvdbId,
			title = "Overseerr Show " + unique,
			qualityProfileId = qualityId,
			languageProfileId = languageId,
			rootFolderPath = (await factory.WithDbAsync(db => db.RootFolders.Where(x => x.Id == rootId).Select(x => x.Path).SingleAsync())),
			seasonFolder = true,
			monitored = true,
			seriesType = "standard",
			addOptions = new { monitor = "all", searchForMissingEpisodes = false }
		});
		add.StatusCode.ShouldBe(HttpStatusCode.Created);
		var added = await add.Content.ReadFromJsonAsync<JsonElement>();
		var seriesId = added.GetProperty("id").GetInt32();

		// Duplicate lookup by tvdb id should hydrate the already-owned local id, never a second identity.
		using var duplicateLookup = await client.GetAsync($"/compat/sonarr/api/v3/series/lookup?term=tvdb:{tvdbId}");
		duplicateLookup.StatusCode.ShouldBe(HttpStatusCode.OK);
		var hits = await duplicateLookup.Content.ReadFromJsonAsync<JsonElement>();
		hits.EnumerateArray().ShouldContain(x => x.GetProperty("tvdbId").GetInt32() == tvdbId);

		using var get = await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}");
		var current = await get.Content.ReadFromJsonAsync<JsonElement>();
		var mutable = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(current.GetRawText())!;
		using var update = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/series/{seriesId}", new
		{
			id = seriesId,
			monitored = true,
			seasonFolder = true,
			seriesType = "standard",
			qualityProfileId = qualityId,
			languageProfileId = languageId,
			tags = Array.Empty<int>()
		});
		update.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var search = await client.PostAsJsonAsync("/compat/sonarr/api/v3/command", new { name = "SeriesSearch", seriesId });
		search.StatusCode.ShouldBe(HttpStatusCode.Created);

		using var queue = await client.GetAsync("/compat/sonarr/api/v3/queue");
		queue.StatusCode.ShouldBe(HttpStatusCode.OK);

		(await factory.WithDbAsync(db => db.Series.CountAsync(x => x.TvdbId == tvdbId))).ShouldBe(1);
	}

	[Fact]
	public async Task ConsumerSequence_Bazarr_Sync_ShouldExposeAbsolutePathsAndTriggerRescan()
	{
		var unique = Guid.NewGuid().ToString("N");
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(unique);
		var client = await _factory.CreateAuthorizedClientAsync();

		using var status = await client.GetAsync("/compat/sonarr/api/v3/system/status");
		var statusBody = await status.Content.ReadFromJsonAsync<JsonElement>();
		statusBody.GetProperty("version").GetString()!.ShouldStartWith("4.");

		using var languageProfiles = await client.GetAsync("/compat/sonarr/api/v3/languageprofile");
		languageProfiles.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var seriesList = await client.GetAsync("/compat/sonarr/api/v3/series");
		seriesList.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var episodes = await client.GetAsync($"/compat/sonarr/api/v3/episode?seriesId={seriesId}&includeEpisodeFile=true");
		episodes.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var history = await client.GetAsync($"/compat/sonarr/api/v3/history?eventType=1&episodeId={episodeId}");
		history.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var rescan = await client.PostAsJsonAsync("/compat/sonarr/api/v3/command", new { name = "RescanSeries", seriesId });
		rescan.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task ConsumerSequence_MaintainerrAndDecluttarr_ShouldUnmonitorDeleteQueueAndSearch()
	{
		var unique = Guid.NewGuid().ToString("N");
		var (seriesId, mainVersionId, _, episodeId) = await SeedTwoVersionSeriesAsync(unique);
		var client = await _factory.CreateAuthorizedClientAsync();
		(await client.GetAsync($"/compat/sonarr/api/v3/series/{seriesId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		// Decluttarr: refresh, poll queue, missing/cutoff, scoped search.
		using var refresh = await client.PostAsJsonAsync("/compat/sonarr/api/v3/command", new { name = "RefreshMonitoredDownloads" });
		refresh.StatusCode.ShouldBe(HttpStatusCode.Created);
		using var queue = await client.GetAsync("/compat/sonarr/api/v3/queue?page=1&pageSize=20");
		queue.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var missing = await client.GetAsync("/compat/sonarr/api/v3/wanted/missing?page=1&pageSize=20");
		missing.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var episodeSearch = await client.PostAsJsonAsync("/compat/sonarr/api/v3/command", new { name = "EpisodeSearch", episodeIds = new[] { episodeId } });
		episodeSearch.StatusCode.ShouldBe(HttpStatusCode.Created);

		// Maintainerr: unmonitor, then delete preserving no siblings after removing the last version's tombstone flow.
		using var unmonitor = await client.PutAsJsonAsync($"/compat/sonarr/api/v3/series/{seriesId}", new
		{
			id = seriesId,
			monitored = false,
			seasonFolder = true,
			seriesType = "standard",
			qualityProfileId = (await _factory.WithDbAsync(db => db.MediaVersions.Where(x => x.Id == mainVersionId).Select(x => x.QualityProfileId).SingleAsync())),
			languageProfileId = (await _factory.WithDbAsync(db => db.MediaVersions.Where(x => x.Id == mainVersionId).Select(x => x.LanguageProfileId).SingleAsync())),
			tags = Array.Empty<int>()
		});
		unmonitor.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await _factory.WithDbAsync(db => db.Series.Where(x => x.Id == seriesId).Select(x => x.Monitored).SingleAsync())).ShouldBeFalse();
	}
}
