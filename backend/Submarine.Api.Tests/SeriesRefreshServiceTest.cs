using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Events;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.Quality;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class SeriesRefreshServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task RefreshSeriesAsync_ShouldPublishTitleChangedOnce_WhenStoredEpisodeTitleChangesAndHasFile()
	{
		var series = await SeedSeriesWithFiledEpisodeAsync("Old Title");
		var publisher = new FakeEventPublisher();
		var metadata = new FakeMetadataClient { Series = BuildResource("New Title") };

		var service = new SeriesRefreshService(Context, metadata, publisher,
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		var published = Assert.Single(publisher.Published);
		var @event = Assert.IsType<EpisodeTitleChangedEvent>(published);
		Assert.Equal("Old Title", @event.OldTitle);
		Assert.Equal("New Title", @event.NewTitle);
	}

	[Fact]
	public async Task RefreshSeriesAsync_ShouldPublishNothing_WhenEpisodeTitleUnchanged()
	{
		var series = await SeedSeriesWithFiledEpisodeAsync("Same Title");
		var publisher = new FakeEventPublisher();
		var metadata = new FakeMetadataClient { Series = BuildResource("Same Title") };

		var service = new SeriesRefreshService(Context, metadata, publisher,
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		Assert.Empty(publisher.Published);
	}

	[Fact]
	public async Task RefreshSeriesAsync_ShouldMaterializeDvdNumbers_WhenNumberingIsDvd()
	{
		var series = new Series { TvdbId = 100, Title = "Show", Monitored = true, Numbering = EpisodeNumbering.DVD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var metadata = new FakeMetadataClient
		{
			Series = ResourceWithEpisode(new EpisodeResource(500, null, "Ep", null, null, 30, new[]
			{
				new EpisodeNumber(EpisodeOrdering.Aired, 1, 3, null),
				new EpisodeNumber(EpisodeOrdering.Dvd, 2, 1, null)
			}))
		};

		var service = new SeriesRefreshService(Context, metadata, new FakeEventPublisher(),
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		var episode = await Context.Episodes.SingleAsync(e => e.SeriesId == series.Id, TestContext.Current.CancellationToken);
		Assert.Equal(2, episode.SeasonNumber);
		Assert.Equal(1, episode.EpisodeNumber);
	}

	[Fact]
	public async Task RefreshSeriesAsync_ShouldFallBackToAired_WhenDvdEntryMissing()
	{
		var series = new Series { TvdbId = 100, Title = "Show", Monitored = true, Numbering = EpisodeNumbering.DVD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var metadata = new FakeMetadataClient
		{
			Series = ResourceWithEpisode(new EpisodeResource(500, null, "Ep", null, null, 30, new[]
			{
				new EpisodeNumber(EpisodeOrdering.Aired, 1, 3, null)
			}))
		};

		var service = new SeriesRefreshService(Context, metadata, new FakeEventPublisher(),
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		var episode = await Context.Episodes.SingleAsync(e => e.SeriesId == series.Id, TestContext.Current.CancellationToken);
		Assert.Equal(1, episode.SeasonNumber);
		Assert.Equal(3, episode.EpisodeNumber);
	}

	[Fact]
	public async Task RefreshSeriesAsync_ShouldRenumberExistingEpisode_WhenNumberingSwitchesWithoutDuplicating()
	{
		var series = await SeedSeriesWithFiledEpisodeAsync("Ep", 500);
		series.Numbering = EpisodeNumbering.DVD;

		var metadata = new FakeMetadataClient
		{
			Series = ResourceWithEpisode(new EpisodeResource(500, null, "Ep", null, null, 30, new[]
			{
				new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null),
				new EpisodeNumber(EpisodeOrdering.Dvd, 2, 5, null)
			}))
		};

		var service = new SeriesRefreshService(Context, metadata, new FakeEventPublisher(),
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		var episode = await Context.Episodes.Include(e => e.Files).SingleAsync(e => e.SeriesId == series.Id, TestContext.Current.CancellationToken);
		Assert.Equal(2, episode.SeasonNumber);
		Assert.Equal(5, episode.EpisodeNumber);
		Assert.Single(episode.Files);
	}

	[Fact]
	public async Task RefreshSeriesAsync_ShouldFetchFromTmdb_WhenProviderIsTmdb()
	{
		var series = new Series
		{
			TvdbId = 100, TmdbId = 900, Title = "Show", Monitored = true, MetadataProvider = MetadataProvider.TMDB
		};
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var metadata = new FakeMetadataClient { TmdbSeries = BuildResource("Ep") };

		var service = new SeriesRefreshService(Context, metadata, new FakeEventPublisher(),
			NullLogger<SeriesRefreshService>.Instance);

		await service.RefreshSeriesAsync(series, TestContext.Current.CancellationToken);

		Assert.Equal(1, metadata.TmdbSeriesCalls);
		Assert.Equal(0, metadata.TvdbSeriesCalls);
	}

	private async Task<Series> SeedSeriesWithFiledEpisodeAsync(string storedTitle, int? episodeTvdbId = null)
	{
		var series = new Series { TvdbId = 100, Title = "Show", Monitored = true };
		Context.Series.Add(series);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var version = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = Path.Combine(Path.GetTempPath(), "show"),
			QualityProfileId = 1, LanguageProfileId = 1, Monitored = true
		};
		Context.Versions.Add(version);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var file = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "Season 01/episode.mkv",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
				new Revision())
		};
		Context.EpisodeFiles.Add(file);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		Context.Episodes.Add(new Episode
		{
			SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, TvdbId = episodeTvdbId, Title = storedTitle,
			Files = new List<EpisodeFile> { file }
		});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		return series;
	}

	private static SeriesResource BuildResource(string episodeTitle)
		=> ResourceWithEpisode(new EpisodeResource(1, null, episodeTitle, null, null, 30,
			new[] { new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null) }));

	private static SeriesResource ResourceWithEpisode(EpisodeResource episode)
		=> new(100, null, "Show", null, null, null, Submarine.Metadata.Contracts.SeriesStatus.Continuing, 30, null,
			Array.Empty<string>(),
			Array.Empty<SeasonResource>(),
			new[] { episode },
			null, null);
}
