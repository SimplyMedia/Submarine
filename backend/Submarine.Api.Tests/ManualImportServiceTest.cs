using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class ManualImportServiceTest : DatabaseTestBase
{
	private ManualImportService BuildService()
		=> new(Context, new ImportService(Context, Settings(), ReleaseParser(), NamingService(),
			new HistoryService(Context), new FakeEventPublisher(), new FakeMappingsClient(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<ImportService>.Instance), Settings(), ReleaseParser());

	[Fact]
	public async Task ImportAsync_ShouldPlaceFileAndLinkEpisode_WhenFileChosen()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "library", "Show");
		var sourcePath = Path.Combine(root, "source");
		Directory.CreateDirectory(libraryPath);
		Directory.CreateDirectory(sourcePath);
		var sourceFile = Path.Combine(sourcePath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv");
		await File.WriteAllTextAsync(sourceFile, "video");

		try
		{
			Context.MediaManagementConfigs.Add(new Core.Config.MediaManagementConfig { Id = 1, UseHardlinks = false });

			var series = new Series
			{
				TvdbId = 1, Title = "Show", Monitored = true, SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync();

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real" };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync();

			var service = BuildService();

			var response = await service.ImportAsync(new ManualImportRequest
			{
				Files = new List<ManualImportFile>
				{
					new()
					{
						Path = sourceFile, SeriesId = series.Id, EpisodeIds = new List<int> { episode.Id },
						MediaVersionId = version.Id
					}
				}
			});

			var result = Assert.Single(response.Results);
			Assert.True(result.Imported);

			var episodeFile = await Context.EpisodeFiles.SingleAsync();
			Assert.Equal(version.Id, episodeFile.MediaVersionId);
			Assert.True(File.Exists(Path.Combine(libraryPath, episodeFile.RelativePath)));

			var storedEpisode = await Context.Episodes.Include(e => e.Files).SingleAsync();
			Assert.Contains(storedEpisode.Files, f => f.Id == episodeFile.Id);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public async Task ImportAsync_ShouldThrowBadRequest_WhenVersionDoesNotBelongToMedia()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var sourcePath = Path.Combine(root, "source");
		Directory.CreateDirectory(sourcePath);
		var sourceFile = Path.Combine(sourcePath, "Show.S01E05.1080p.WEB-DL.x264-GROUP.mkv");
		await File.WriteAllTextAsync(sourceFile, "video");

		try
		{
			var seriesA = new Series { TvdbId = 1, Title = "A", Type = SeriesType.STANDARD };
			var seriesB = new Series { TvdbId = 2, Title = "B", Type = SeriesType.STANDARD };
			Context.Series.AddRange(seriesA, seriesB);
			await Context.SaveChangesAsync();

			var versionB = new MediaVersion
			{
				SeriesId = seriesB.Id, Name = "Default", Path = Path.Combine(root, "B"), QualityProfileId = 1,
				LanguageProfileId = 1
			};
			Context.Versions.Add(versionB);
			var episode = new Episode { SeriesId = seriesA.Id, SeasonNumber = 1, EpisodeNumber = 5 };
			Context.Episodes.Add(episode);
			await Context.SaveChangesAsync();

			var service = BuildService();

			await Assert.ThrowsAsync<BadRequestException>(() => service.ImportAsync(new ManualImportRequest
			{
				Files = new List<ManualImportFile>
				{
					new()
					{
						Path = sourceFile, SeriesId = seriesA.Id, EpisodeIds = new List<int> { episode.Id },
						MediaVersionId = versionB.Id
					}
				}
			}));

			Assert.Empty(await Context.EpisodeFiles.ToListAsync());
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
