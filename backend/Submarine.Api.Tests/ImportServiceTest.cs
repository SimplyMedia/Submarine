using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.Download;
using Submarine.Core.History;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
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
		await File.WriteAllTextAsync(Path.Combine(downloadPath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv"), "video");

		try
		{
			Context.MediaManagementConfigs.Add(new MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Path = libraryPath, Monitored = true, SeasonFolder = true,
				Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync();

			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = null };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync();

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
				EpisodeIds = new List<int> { episode.Id },
				OutputPath = downloadPath
			};
			Context.TrackedDownloads.Add(tracked);
			await Context.SaveChangesAsync();

			var service = new ImportService(Context, new SettingsService(Context), ReleaseParser(), NamingService(),
				new HistoryService(Context), NullLogger<ImportService>.Instance);

			await service.ImportTrackedDownloadAsync(tracked.Id);

			var file = await Context.EpisodeFiles.SingleAsync();
			Assert.True(file.NamedFromPlaceholder);
			Assert.Equal(Path.Combine("Season 01", "Show - S01E05 - Episode 5.mkv"), file.RelativePath);
			Assert.True(File.Exists(Path.Combine(libraryPath, file.RelativePath)));

			var storedEpisode = await Context.Episodes.SingleAsync();
			Assert.Equal(file.Id, storedEpisode.EpisodeFileId);

			var storedTracked = await Context.TrackedDownloads.SingleAsync();
			Assert.True(storedTracked.Imported);

			var history = await Context.History.SingleAsync();
			Assert.Equal(HistoryEventType.IMPORTED, history.Type);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
