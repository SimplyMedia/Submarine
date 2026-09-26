using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Languages;
using Submarine.Core.Naming;
using Submarine.Core.MediaFiles;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.MediaFiles;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Import;

public sealed class ImportServiceTests : IDisposable
{
	private readonly string _tempDir = Directory.CreateTempSubdirectory("submarine-importsvc-").FullName;
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly ImportService _service;

	public ImportServiceTests()
	{
		_db = TestDbFactory.Create(_time);

		var mediaInfo = Substitute.For<IMediaInfoService>();
		mediaInfo.ProbeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((MediaInfoModel?)null);

		_service = new global::Submarine.Infrastructure.Import.ImportService(
			_db,
			TestReleaseParserFactory.Create(),
			new NamingService(),
			new global::Submarine.Infrastructure.Import.FileLinker(NullLogger<global::Submarine.Infrastructure.Import.FileLinker>.Instance),
			new global::Submarine.Infrastructure.Import.RecycleBinService(),
			Substitute.For<global::Submarine.Infrastructure.Metadata.IMetadataConsumerWriter>(),
			Substitute.For<global::Submarine.Infrastructure.Import.IFileDateService>(),
			mediaInfo,
			_eventBus,
			_time,
			NullLogger<global::Submarine.Infrastructure.Import.ImportService>.Instance);
	}

	public void Dispose()
	{
		_db.Dispose();
		Directory.Delete(_tempDir, recursive: true);
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldImport_WhenNoExistingFile()
	{
		var (series, version, root, episode1, _) = SeedSeries();
		var downloadFolder = Path.Combine(_tempDir, "downloads", "Test.Show.S01E01.1080p.BluRay.x264-GROUP");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.mkv"), "video");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeTrue();
		var files = await _db.EpisodeFiles.Include(x => x.Episodes).ToListAsync(TestContext.Current.CancellationToken);
		files.Count.ShouldBe(1);
		files[0].Episodes.Single().Id.ShouldBe(episode1.Id);
		File.Exists(Path.Combine(root.Path, version.Path, "Season 01", "Test Show - S01E01 - Pilot.mkv")).ShouldBeTrue();
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldReject_WhenExistingFileIsSameQuality()
	{
		var (series, version, _, episode1, _) = SeedSeries();
		var existing = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "existing.mkv",
			Quality = BluRay720,
			Languages = [Language.ENGLISH],
			DateAdded = _time.GetUtcNow().UtcDateTime
		};
		existing.Episodes.Add(episode1);
		_db.EpisodeFiles.Add(existing);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var downloadFolder = Path.Combine(_tempDir, "downloads", "same");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.720p.BluRay.x264-GROUP.mkv"), "video");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeFalse();
		summary.Files.Single().Rejection.ShouldBe(ImportRejectionReason.SAME_OR_WORSE_QUALITY);
		(await _db.EpisodeFiles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldUpgrade_WhenExistingFileIsLowerQuality()
	{
		var (series, version, root, episode1, _) = SeedSeries();
		var existing = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "existing.mkv",
			Quality = BluRay720,
			Languages = [Language.ENGLISH],
			DateAdded = _time.GetUtcNow().UtcDateTime
		};
		existing.Episodes.Add(episode1);
		_db.EpisodeFiles.Add(existing);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var downloadFolder = Path.Combine(_tempDir, "downloads", "upgrade");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.mkv"), "video");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeTrue();
		summary.Files.Single().IsUpgrade.ShouldBeTrue();
		var remaining = await _db.EpisodeFiles.ToListAsync(TestContext.Current.CancellationToken);
		remaining.Count.ShouldBe(1);
		remaining[0].Quality.Resolution.Resolution.ShouldBe(QualityResolution.R1080_P);
		await _eventBus.Received().PublishAsync(Arg.Any<EpisodeFileDeletedEvent>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldSkipSampleFile_WhenOtherMediaFilesExist()
	{
		var (series, version, _, episode1, _) = SeedSeries();
		var downloadFolder = Path.Combine(_tempDir, "downloads", "withsample");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.mkv"), "video");
		File.WriteAllBytes(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.sample.mkv"), new byte[1024]);
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(1);
		summary.AnyImported.ShouldBeTrue();
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldMatchSeasonPack_ToEachEpisodeByFileName()
	{
		var (series, version, root, episode1, episode2) = SeedSeries();
		var downloadFolder = Path.Combine(_tempDir, "downloads", "seasonpack");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.mkv"), "video1");
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E02.1080p.BluRay.x264-GROUP.mkv"), "video2");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id, episode2.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(2);
		summary.Files.ShouldAllBe(x => x.Imported);
		var files = await _db.EpisodeFiles.Include(x => x.Episodes).ToListAsync(TestContext.Current.CancellationToken);
		files.Count.ShouldBe(2);
		files.SelectMany(x => x.Episodes.Select(e => e.Id)).OrderBy(x => x).ShouldBe([episode1.Id, episode2.Id]);
	}

	[Fact]
	public async Task ImportManualAsync_ShouldForceImport_BypassingQualityGate()
	{
		var (series, version, root, episode1, _) = SeedSeries();
		var existing = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "existing.mkv",
			Quality = BluRay1080,
			Languages = [Language.ENGLISH],
			DateAdded = _time.GetUtcNow().UtcDateTime
		};
		existing.Episodes.Add(episode1);
		_db.EpisodeFiles.Add(existing);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var folder = Path.Combine(_tempDir, "manual");
		Directory.CreateDirectory(folder);
		var filePath = Path.Combine(folder, "Test.Show.S01E01.720p.BluRay.x264-GROUP.mkv");
		File.WriteAllText(filePath, "video");

		var selection = new global::Submarine.Infrastructure.Import.ManualImportSelection(filePath, series.Id, [episode1.Id], null, version.Id, null, null, null, null);
		var summary = await _service.ImportManualAsync([selection], global::Submarine.Infrastructure.Import.ImportMode.MOVE, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeTrue();
		var remaining = await _db.EpisodeFiles.ToListAsync(TestContext.Current.CancellationToken);
		remaining.Count.ShouldBe(1);
		remaining[0].Quality.Resolution.Resolution.ShouldBe(QualityResolution.R720_P);
	}

	[Fact]
	public async Task RescanSeriesAsync_ShouldRemoveRowForVanishedFile_AndAdoptUntrackedFile()
	{
		var (series, version, root, episode1, episode2) = SeedSeries();
		var versionFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(versionFolder);

		// pre-existing DB row whose file has vanished from disk
		var vanished = new EpisodeFile { SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = "gone.mkv", Quality = BluRay1080, Languages = [Language.ENGLISH], DateAdded = _time.GetUtcNow().UtcDateTime };
		vanished.Episodes.Add(episode1);
		_db.EpisodeFiles.Add(vanished);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		// untracked file already sitting in the version folder
		File.WriteAllText(Path.Combine(versionFolder, "Test.Show.S01E02.1080p.BluRay.x264-GROUP.mkv"), "video");

		var summary = await _service.RescanSeriesAsync(series.Id, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeTrue();
		var files = await _db.EpisodeFiles.Include(x => x.Episodes).ToListAsync(TestContext.Current.CancellationToken);
		files.Count.ShouldBe(1);
		files[0].Episodes.Single().Id.ShouldBe(episode2.Id);
		await _eventBus.Received().PublishAsync(Arg.Is<EpisodeFileDeletedEvent>(x => x.Reason == FileDeleteReason.MISSING_FROM_DISK), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RescanSeriesAsync_ShouldSkipDeletion_WhenVersionFolderEnumeratesZeroFiles()
	{
		var (series, version, root, episode1, _) = SeedSeries();
		var versionFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(versionFolder);

		var registered = new EpisodeFile { SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = "gone.mkv", Quality = BluRay1080, Languages = [Language.ENGLISH], DateAdded = _time.GetUtcNow().UtcDateTime };
		registered.Episodes.Add(episode1);
		_db.EpisodeFiles.Add(registered);
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		// versionFolder exists but enumerates zero files, e.g. an unmounted bind-mount.
		var summary = await _service.RescanSeriesAsync(series.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(0);
		(await _db.EpisodeFiles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldImportOnlyOneFile_WhenBothFilesLackParsedEpisodeNumbers()
	{
		var (series, version, _, episode1, _) = SeedSeries();
		var downloadFolder = Path.Combine(_tempDir, "downloads", "nonumbers");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "video1.mkv"), "video1");
		File.WriteAllText(Path.Combine(downloadFolder, "video2.mkv"), "video2");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(2);
		summary.Files.Count(x => x.Imported).ShouldBe(1);
		(await _db.EpisodeFiles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldSetFailedPending_WhenNoImportableFilesFound()
	{
		var (series, version, _, episode1, _) = SeedSeries();
		var downloadFolder = Path.Combine(_tempDir, "downloads", "empty");
		Directory.CreateDirectory(downloadFolder);
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(0);
		var reloaded = await _db.TrackedDownloads.SingleAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken);
		reloaded.State.ShouldBe(TrackedDownloadState.FAILED_PENDING);
		reloaded.StatusMessages.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldSetFailedPending_WhenVersionPathEscapesRoot()
	{
		var (series, version, _, episode1, _) = SeedSeries();
		version.Path = "../escaped";
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var downloadFolder = Path.Combine(_tempDir, "downloads", "escape");
		Directory.CreateDirectory(downloadFolder);
		File.WriteAllText(Path.Combine(downloadFolder, "Test.Show.S01E01.1080p.BluRay.x264-GROUP.mkv"), "video");
		var download = SeedTrackedDownload(series.Id, version.Id, [episode1.Id], downloadFolder);

		var summary = await _service.ImportTrackedDownloadAsync(download.Id, TestContext.Current.CancellationToken);

		summary.Files.Count.ShouldBe(0);
		var reloaded = await _db.TrackedDownloads.SingleAsync(x => x.Id == download.Id, TestContext.Current.CancellationToken);
		reloaded.State.ShouldBe(TrackedDownloadState.FAILED_PENDING);
		reloaded.StatusMessages.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ImportManualAsync_ShouldNotDestroySource_WhenSourceEqualsComputedDestination()
	{
		var (series, version, root, episode1, _) = SeedSeries();
		series.SeasonFolder = false;
		var namingConfig = await _db.NamingConfig.SingleAsync(TestContext.Current.CancellationToken);
		namingConfig.RenameEpisodes = false;
		await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var versionFolder = Path.Combine(root.Path, version.Path);
		Directory.CreateDirectory(versionFolder);
		var filePath = Path.Combine(versionFolder, "already-here.mkv");
		File.WriteAllText(filePath, "video");

		var selection = new global::Submarine.Infrastructure.Import.ManualImportSelection(filePath, series.Id, [episode1.Id], null, version.Id, null, null, null, null);
		var summary = await _service.ImportManualAsync([selection], global::Submarine.Infrastructure.Import.ImportMode.MOVE, TestContext.Current.CancellationToken);

		summary.AnyImported.ShouldBeTrue();
		File.Exists(filePath).ShouldBeTrue();
		File.ReadAllText(filePath).ShouldBe("video");
		(await _db.EpisodeFiles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	private static readonly QualityModel BluRay720 = new(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R720_P), new Revision());
	private static readonly QualityModel BluRay1080 = new(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R1080_P), new Revision());

	private (Series Series, MediaVersion Version, RootFolder Root, Episode Episode1, Episode Episode2) SeedSeries()
	{
		var root = new RootFolder { Path = Path.Combine(_tempDir, "library"), MediaKind = MediaKind.SERIES };
		Directory.CreateDirectory(root.Path);

		var quality = new QualityProfile
		{
			Name = "Test",
			UpgradeAllowed = true,
			Cutoff = 2,
			Items =
			[
				new QualityProfileItem(new QualityResolutionModel(QualitySource.TV), true),
				new QualityProfileItem(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R720_P), true),
				new QualityProfileItem(new QualityResolutionModel(QualitySource.BLURAY, QualityResolution.R1080_P), true)
			]
		};
		var language = new LanguageProfile { Name = "English", Languages = [Language.ENGLISH], Cutoff = Language.ENGLISH, UpgradeAllowed = true };

		_db.RootFolders.Add(root);
		_db.QualityProfiles.Add(quality);
		_db.LanguageProfiles.Add(language);
		_db.MediaManagementConfig.Add(new MediaManagementConfig { UseHardlinks = false, ImportExtraFiles = false, EnableMediaInfo = false, DeleteEmptyFolders = true, UnmonitorDeletedFiles = false });
		_db.NamingConfig.Add(new NamingConfig());
		_db.SaveChanges();

		var series = new Series { Title = "Test Show", SortTitle = "test show", CleanTitle = "testshow", Type = SeriesType.STANDARD, Numbering = SeriesNumbering.AIRED, SeasonFolder = true };
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot", Monitored = true });
		series.Episodes.Add(new Episode { SeasonNumber = 1, EpisodeNumber = 2, Title = "Second", Monitored = true });
		_db.Series.Add(series);
		_db.SaveChanges();

		var version = new MediaVersion { SeriesId = series.Id, Name = "Default", QualityProfileId = quality.Id, LanguageProfileId = language.Id, RootFolderId = root.Id, Path = "Test Show" };
		_db.MediaVersions.Add(version);
		_db.SaveChanges();

		var episodes = series.Episodes.OrderBy(x => x.EpisodeNumber).ToList();
		return (series, version, root, episodes[0], episodes[1]);
	}

	private TrackedDownload SeedTrackedDownload(int seriesId, int versionId, IReadOnlyList<int> episodeIds, string outputPath)
	{
		var client = new DownloadClient { Name = "Client", Type = DownloadClientType.QBITTORRENT, SettingsJson = "{}" };
		_db.DownloadClients.Add(client);
		_db.SaveChanges();

		var download = new TrackedDownload
		{
			DownloadClientId = client.Id,
			DownloadId = Guid.NewGuid().ToString("N"),
			Title = "Test.Show.S01E01.1080p.BluRay.x264-GROUP",
			Protocol = Protocol.BITTORRENT,
			Status = TrackedDownloadStatus.COMPLETED,
			State = TrackedDownloadState.IMPORT_PENDING,
			SeriesId = seriesId,
			MediaVersionId = versionId,
			EpisodeIds = [.. episodeIds],
			OutputPath = outputPath,
			Added = _time.GetUtcNow().UtcDateTime
		};
		_db.TrackedDownloads.Add(download);
		_db.SaveChanges();
		return download;
	}
}
