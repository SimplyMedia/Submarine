using Microsoft.EntityFrameworkCore;
using Submarine.Api.Jobs;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Core.History;
using Submarine.Core.MediaFile;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Api.Tests;

public class RenameServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task RenameEpisodeFileAsync_ShouldMoveFileClearPlaceholderAndRecordHistory_WhenTitleResolved()
	{
		var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var libraryPath = Path.Combine(root, "Show");
		var oldRelative = Path.Combine("Season 01", "Show - S01E05 - Episode 5.mkv");
		Directory.CreateDirectory(Path.Combine(libraryPath, "Season 01"));
		await File.WriteAllTextAsync(Path.Combine(libraryPath, oldRelative), "video");

		try
		{
			var series = new Series
			{
				TvdbId = 1, Title = "Show", SeasonFolder = true, Type = SeriesType.STANDARD
			};
			Context.Series.Add(series);
			await Context.SaveChangesAsync();

			var version = new MediaVersion
			{
				SeriesId = series.Id, Name = "Default", Path = libraryPath, QualityProfileId = 1,
				LanguageProfileId = 1, Monitored = true
			};
			Context.Versions.Add(version);
			await Context.SaveChangesAsync();

			var file = new EpisodeFile
			{
				SeriesId = series.Id,
				MediaVersionId = version.Id,
				RelativePath = oldRelative,
				NamedFromPlaceholder = true,
				Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
					new Revision())
			};
			Context.EpisodeFiles.Add(file);
			await Context.SaveChangesAsync();

			Context.Episodes.Add(new Episode
			{
				SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Title = "Real Title",
				Files = new List<EpisodeFile> { file }
			});
			await Context.SaveChangesAsync();

			var service = new RenameService(Context, new SettingsService(Context), NamingService(),
				new HistoryService(Context), new ChannelBackgroundTaskQueue(), new FakeEventPublisher());

			await service.RenameEpisodeFileAsync(file.Id);

			var newRelative = Path.Combine("Season 01", "Show - S01E05 - Real Title.mkv");

			var stored = await Context.EpisodeFiles.SingleAsync();
			Assert.Equal(newRelative, stored.RelativePath);
			Assert.False(stored.NamedFromPlaceholder);
			Assert.True(File.Exists(Path.Combine(libraryPath, newRelative)));
			Assert.False(File.Exists(Path.Combine(libraryPath, oldRelative)));

			var history = await Context.History.SingleAsync();
			Assert.Equal(HistoryEventType.RENAMED, history.Type);
			Assert.Equal(oldRelative, history.SourceTitle);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
