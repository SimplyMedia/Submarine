using Submarine.Api.Services;
using Submarine.Core.History;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Api.Tests;

public class StatsServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task GetStatsAsync_ShouldReturnCorrectCounts_WhenLibraryIsSeeded()
	{
		var continuing = new Series
		{
			TvdbId = 1, Title = "Continuing Show", Type = SeriesType.STANDARD, Status = SeriesStatus.CONTINUING
		};
		var ended = new Series { TvdbId = 2, Title = "Ended Show", Type = SeriesType.STANDARD, Status = SeriesStatus.ENDED };
		Context.Series.AddRange(continuing, ended);
		var movie = new Movie { TmdbId = 1, Title = "Movie" };
		Context.Movies.Add(movie);
		await Context.SaveChangesAsync();

		var version = new MediaVersion
		{
			SeriesId = continuing.Id, Name = "Default", Path = "/lib/continuing", QualityProfileId = 1,
			LanguageProfileId = 1, Monitored = true
		};
		var movieVersion = new MediaVersion
		{
			MovieId = movie.Id, Name = "Default", Path = "/lib/movie", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		};
		Context.Versions.AddRange(version, movieVersion);
		var episode1 = new Episode { SeriesId = continuing.Id, SeasonNumber = 1, EpisodeNumber = 1 };
		var episode2 = new Episode { SeriesId = continuing.Id, SeasonNumber = 1, EpisodeNumber = 2 };
		Context.Episodes.AddRange(episode1, episode2);
		await Context.SaveChangesAsync();

		var quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
			new Revision());

		Context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = continuing.Id, MediaVersionId = version.Id, RelativePath = "ep1.mkv", Size = 1000,
			Quality = quality, Languages = new List<Language> { Language.ENGLISH },
			Episodes = new List<Episode> { episode1 }
		});
		Context.MovieFiles.Add(new MovieFile
		{
			MovieId = movie.Id, MediaVersionId = movieVersion.Id, RelativePath = "movie.mkv", Size = 2000,
			Quality = quality, Languages = new List<Language> { Language.ENGLISH }
		});
		Context.RootFolders.Add(new RootFolder { Path = Path.GetTempPath(), MediaKind = MediaKind.SERIES });

		var oldEvent = new HistoryEvent { Type = HistoryEventType.GRABBED, SourceTitle = "old" };
		Context.History.AddRange(
			new HistoryEvent { Type = HistoryEventType.GRABBED, SourceTitle = "a" },
			new HistoryEvent { Type = HistoryEventType.GRABBED, SourceTitle = "b" },
			new HistoryEvent { Type = HistoryEventType.IMPORTED, SourceTitle = "c" },
			new HistoryEvent { Type = HistoryEventType.FAILED, SourceTitle = "d" },
			oldEvent);
		await Context.SaveChangesAsync();

		// CreatedAt is stamped to "now" on insert regardless of the value set above, back-date it with a second
		// save so it falls outside the 30 day window
		oldEvent.CreatedAt = DateTimeOffset.UtcNow.AddDays(-31);
		await Context.SaveChangesAsync();

		var service = new StatsService(Context);

		var stats = await service.GetStatsAsync();

		Assert.Equal(2, stats.SeriesCount);
		Assert.Equal(1, stats.EndedSeriesCount);
		Assert.Equal(1, stats.ContinuingSeriesCount);
		Assert.Equal(1, stats.MovieCount);
		Assert.Equal(2, stats.EpisodeCount);
		Assert.Equal(1, stats.EpisodeFileCount);
		Assert.Equal(1, stats.MovieFileCount);
		Assert.Equal(3000, stats.TotalFileSize);
		Assert.Single(stats.PerRootFolder);
		Assert.Equal(2, stats.HistoryCounts.Grabbed);
		Assert.Equal(1, stats.HistoryCounts.Imported);
		Assert.Equal(1, stats.HistoryCounts.Failed);
	}
}
