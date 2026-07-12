using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.Download;
using Submarine.Core.History;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Mappings.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class ImportServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldRenameFromPlaceholderAndFlagFile_WhenEpisodeHasNoTitle()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = null };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			Assert.True(file.NamedFromPlaceholder);
			Assert.Equal(version.Id, file.MediaVersionId);
			Assert.Equal(Path.Combine("Season 01", "Show - S01E05 - Episode 5.mkv"), file.RelativePath);
			Assert.True(File.Exists(Path.Combine(libraryPath, file.RelativePath)));

			var storedEpisode = await Context.Episodes.Include(e => e.Files).SingleAsync(TestContext.Current.CancellationToken);
			Assert.Contains(storedEpisode.Files, f => f.Id == file.Id);

			var storedTracked = await Context.TrackedDownloads.SingleAsync(TestContext.Current.CancellationToken);
			Assert.True(storedTracked.Imported);

			var history = await Context.History.SingleAsync(TestContext.Current.CancellationToken);
			Assert.Equal(HistoryEventType.IMPORTED, history.Type);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldReplaceOldFileInSameVersion_WhenUpgrading()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(Path.Combine(libraryPath, "Season 01"));
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.2160p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);
		var oldRelative = Path.Combine("Season 01", "Show - S01E05 - OldName.mkv");
		await File.WriteAllTextAsync(Path.Combine(libraryPath, oldRelative), "old", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			Context.EpisodeFiles.Add(new EpisodeFile
			{
				SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = oldRelative,
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH }, Episodes = new List<Episode> { episode }
			});
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 2160p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R2160_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			Assert.Equal(QualityResolution.R2160_P, file.Quality.Resolution.Resolution);
			Assert.Equal("video", await File.ReadAllTextAsync(Path.Combine(libraryPath, file.RelativePath), TestContext.Current.CancellationToken));
			Assert.False(File.Exists(Path.Combine(libraryPath, oldRelative)));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldDoNothing_WhenAlreadyImported()
	{
		Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

		var series = new Series { TvdbId = 1, Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var tracked = new TrackedDownload
		{
			DownloadClientConfigId = 1,
			DownloadId = "abc",
			Title = "Show S01E05",
			Protocol = Protocol.BITTORRENT,
			Status = DownloadItemStatus.COMPLETED,
			ReleaseTitle = "Show S01E05",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
				new Revision()),
			Languages = new List<Language> { Language.ENGLISH },
			SeriesId = series.Id,
			EpisodeIds = new List<int>(),
			OutputPath = "does-not-matter",
			Imported = true
		};
		Context.TrackedDownloads.Add(tracked);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var publisher = new FakeEventPublisher();
		var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
			new HistoryService(Context), publisher, new FakeMappingsClient(), new FakeMediaInfoService(),
			NullLogger<ImportService>.Instance);

		await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

		Assert.Empty(await Context.EpisodeFiles.ToListAsync(TestContext.Current.CancellationToken));
		Assert.Empty(publisher.Published);
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldMatchSeasonEpisodeViaAniListMapping_WhenSeriesIsAnime()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Anime");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "[HatSubs] Anime Title 25 [E63F2984].mkv"), "video", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 100, Title = "Anime", Monitored = true, SeasonFolder = true, Type = SeriesType.ANIME
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);

			// Absolute number 25 maps into season 2 episode 12; no episode carries the absolute number itself,
			// so a match can only come from the mapping.
			var episode = new Episode
			{
				SeriesId = series.Id, SeasonNumber = 2, EpisodeNumber = 12, Title = "Twelfth"
			};
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Anime Title 25",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "[HatSubs] Anime Title 25",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int>(),
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var mappings = new FakeMappingsClient
			{
				AniListMappings = new[]
				{
					new AniListMappingResource(1, 100, "Arc 2", TvdbSeason: 2, EpisodeStart: 1, EpisodeCount: 12,
						AbsoluteOffset: 13)
				}
			};

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), mappings, new FakeMediaInfoService(),
					NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			var storedEpisode = await Context.Episodes.Include(e => e.Files).SingleAsync(TestContext.Current.CancellationToken);
			Assert.Contains(storedEpisode.Files, f => f.Id == file.Id);
			Assert.True(File.Exists(Path.Combine(libraryPath, file.RelativePath)));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldRouteIntoGrabbedVersionAndKeepOtherVersionFile_WhenTwoVersions()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var pathA = Path.Combine(root, "1080p", "Show");
		var pathB = Path.Combine(root, "4K", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(pathA);
		Directory.CreateDirectory(Path.Combine(pathB, "Season 01"));
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);
		var existingBRelative = Path.Combine("Season 01", "Show - S01E05.mkv");
		await File.WriteAllTextAsync(Path.Combine(pathB, existingBRelative), "keep", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var versionA = new MediaVersion
			{
				SeriesId = series.Id, Name = "1080p", Path = pathA, QualityProfileId = 1, LanguageProfileId = 1,
				Monitored = true
			};
			var versionB = new MediaVersion
			{
				SeriesId = series.Id, Name = "4K", Path = pathB, QualityProfileId = 1, LanguageProfileId = 1,
				Monitored = true
			};
			Context.Versions.AddRange(versionA, versionB);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			Context.EpisodeFiles.Add(new EpisodeFile
			{
				SeriesId = series.Id, MediaVersionId = versionB.Id, RelativePath = existingBRelative,
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R2160_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH }, Episodes = new List<Episode> { episode }
			});
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = versionA.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var files = await Context.EpisodeFiles.OrderBy(f => f.MediaVersionId).ToListAsync(TestContext.Current.CancellationToken);
			Assert.Equal(2, files.Count);

			var importedIntoA = files.Single(f => f.MediaVersionId == versionA.Id);
			Assert.True(File.Exists(Path.Combine(pathA, importedIntoA.RelativePath)));

			// version B's pre-existing file must be untouched
			Assert.True(File.Exists(Path.Combine(pathB, existingBRelative)));
			Assert.Contains(files, f => f.MediaVersionId == versionB.Id && f.RelativePath == existingBRelative);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldStoreMediaInfoAndCopySidecars_WhenImportExtraFilesEnabled()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.srt"), "sub", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.en.srt"), "sub-en", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig
			{
				Id = 1, UseHardlinks = false, ImportExtraFiles = true
			});

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			const string probeJson =
				"{ \"streams\": [ { \"codec_type\": \"video\", \"codec_name\": \"h264\", \"width\": 1920, \"height\": 1080 } ] }";

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(probeJson), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			Assert.NotNull(file.MediaInfo);
			Assert.Equal("h264", file.MediaInfo.VideoCodec);
			Assert.Equal(1920, file.MediaInfo.Width);
			Assert.Equal(1080, file.MediaInfo.Height);

			var seasonPath = Path.Combine(libraryPath, "Season 01");
			Assert.True(File.Exists(Path.Combine(seasonPath, "Show - S01E05 - Real.srt")));
			Assert.True(File.Exists(Path.Combine(seasonPath, "Show - S01E05 - Real.en.srt")));

			// sidecars are copied, not moved; source cleanup belongs to the download client
			Assert.True(File.Exists(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.srt")));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldRenderNameWithMediaInfoToken_WhenTemplateUsesCodec()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });
			Context.NamingConfigs.Add(new NamingConfig
			{
				Id = 1,
				StandardEpisodeFormat = "{Series Title} - S{Season:00}E{Episode:00} - {Episode Title} {MediaInfo VideoCodec}"
			});

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			const string probeJson =
				"{ \"streams\": [ { \"codec_type\": \"video\", \"codec_name\": \"h264\" } ] }";

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(probeJson), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			Assert.Equal(Path.Combine("Season 01", "Show - S01E05 - Real h264.mkv"), file.RelativePath);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldSkipSidecars_WhenImportExtraFilesDisabled()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.srt"), "sub", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			Assert.Null(file.MediaInfo);
			Assert.False(File.Exists(Path.Combine(libraryPath, "Season 01", "Show - S01E05 - Real.srt")));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldWriteNfoFiles_WhenWriteNfoEnabled()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false, WriteNfo = true });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			var episodeNfoPath = Path.ChangeExtension(Path.Combine(libraryPath, file.RelativePath), ".nfo");
			Assert.True(File.Exists(episodeNfoPath));

			var doc = System.Xml.Linq.XDocument.Parse(await File.ReadAllTextAsync(episodeNfoPath, TestContext.Current.CancellationToken));
			Assert.Equal("Real", doc.Root!.Element("title")!.Value);
			Assert.Equal("1", doc.Root.Element("season")!.Value);
			Assert.Equal("5", doc.Root.Element("episode")!.Value);

			Assert.True(File.Exists(Path.Combine(libraryPath, "tvshow.nfo")));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportTrackedDownloadAsync_ShouldSkipNfoFiles_WhenWriteNfoDisabled()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var downloadPath = Path.Combine(root, "download");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(downloadPath);
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video", TestContext.Current.CancellationToken);

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false, WriteNfo = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var tracked = new TrackedDownload
			{
				DownloadClientConfigId = 1,
				DownloadId = "abc",
				Title = "Show S01E05",
				Protocol = Protocol.BITTORRENT,
				Status = DownloadItemStatus.COMPLETED,
				ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision()),
				Languages = new List<Language> { Language.ENGLISH },
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var service = new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
				new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
				new FakeMediaInfoService(), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id, TestContext.Current.CancellationToken);

			var file = await Context.EpisodeFiles.SingleAsync(TestContext.Current.CancellationToken);
			var episodeNfoPath = Path.ChangeExtension(Path.Combine(libraryPath, file.RelativePath), ".nfo");
			Assert.False(File.Exists(episodeNfoPath));
			Assert.False(File.Exists(Path.Combine(libraryPath, "tvshow.nfo")));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
