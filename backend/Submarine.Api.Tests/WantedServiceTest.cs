using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Profile;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Api.Tests;

public class WantedServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task GetMissingAsync_ShouldListAiredEpisodeAndReleasedMovieWithoutFile_WhenMonitored()
	{
		var series = new Series { Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		var movie = new Movie
		{
			TmdbId = 5, Title = "Film", Monitored = true, ReleaseDate = DateTimeOffset.UtcNow.AddDays(-30)
		};
		Context.Movies.Add(movie);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var seriesVersion = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		};
		var movieVersion = new MediaVersion
		{
			MovieId = movie.Id, Name = "Default", Path = "/lib/Film", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		};
		Context.Versions.AddRange(seriesVersion, movieVersion);

		var missing = new Episode
		{
			SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true,
			AirDate = DateTimeOffset.UtcNow.AddDays(-1)
		};
		var present = new Episode
		{
			SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true,
			AirDate = DateTimeOffset.UtcNow.AddDays(-1)
		};
		Context.Episodes.AddRange(missing, present);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		Context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = series.Id, MediaVersionId = seriesVersion.Id, RelativePath = "S01E02.mkv",
			Quality = Quality(QualityResolution.R1080_P), Episodes = new List<Episode> { present }
		});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var result = await new WantedService(Context, new FakeBackgroundTaskQueue()).GetMissingAsync(1, 50, TestContext.Current.CancellationToken);

		Assert.Equal(2, result.TotalItems);
		var episodeItem = result.Items.Single(i => i.Kind == MediaKind.SERIES);
		Assert.Equal(missing.Id, episodeItem.EpisodeId);
		Assert.Equal(new[] { "Default" }, episodeItem.MissingVersions);
		Assert.Contains(result.Items, i => i.Kind == MediaKind.MOVIES && i.MediaId == movie.Id);
	}

	[Fact]
	public async Task GetCutoffAsync_ShouldListFilesBelowCutoff_WhenQualityUnmet()
	{
		Context.QualityProfiles.Add(new QualityProfile
		{
			Id = 1, Name = "HD", Cutoff = 1,
			Items = new List<QualityProfileItem>
			{
				new() { Quality = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R720_P), Allowed = true },
				new() { Quality = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P), Allowed = true }
			}
		});

		var series = new Series { Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		};
		Context.Versions.Add(version);
		var below = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true };
		var met = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true };
		Context.Episodes.AddRange(below, met);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		Context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = "S01E01.mkv",
			Quality = Quality(QualityResolution.R720_P), Episodes = new List<Episode> { below }
		});
		Context.EpisodeFiles.Add(new EpisodeFile
		{
			SeriesId = series.Id, MediaVersionId = version.Id, RelativePath = "S01E02.mkv",
			Quality = Quality(QualityResolution.R1080_P), Episodes = new List<Episode> { met }
		});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var result = await new WantedService(Context, new FakeBackgroundTaskQueue()).GetCutoffAsync(1, 50, TestContext.Current.CancellationToken);

		var item = Assert.Single(result.Items);
		Assert.Equal(below.Id, item.EpisodeId);
		Assert.Equal("Default", item.VersionName);
	}

	private static QualityModel Quality(QualityResolution resolution)
		=> new(new QualityResolutionModel(QualitySource.WEB_DL, resolution), new Revision());
}
