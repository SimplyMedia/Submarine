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

		await service.RefreshSeriesAsync(series);

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

		await service.RefreshSeriesAsync(series);

		Assert.Empty(publisher.Published);
	}

	private async Task<Series> SeedSeriesWithFiledEpisodeAsync(string storedTitle)
	{
		var series = new Series { TvdbId = 100, Title = "Show", Monitored = true };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var version = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = Path.Combine(Path.GetTempPath(), "show"),
			QualityProfileId = 1, LanguageProfileId = 1, Monitored = true
		};
		Context.Versions.Add(version);
		await Context.SaveChangesAsync();

		var file = new EpisodeFile
		{
			SeriesId = series.Id,
			MediaVersionId = version.Id,
			RelativePath = "Season 01/episode.mkv",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.TV, QualityResolution.R1080_P),
				new Revision())
		};
		Context.EpisodeFiles.Add(file);
		await Context.SaveChangesAsync();

		Context.Episodes.Add(new Episode
		{
			SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Title = storedTitle,
			Files = new List<EpisodeFile> { file }
		});
		await Context.SaveChangesAsync();

		return series;
	}

	private static SeriesResource BuildResource(string episodeTitle)
		=> new(100, null, "Show", null, null, null, Submarine.Metadata.Contracts.SeriesStatus.Continuing, 30, null,
			Array.Empty<string>(),
			Array.Empty<SeasonResource>(),
			new[]
			{
				new EpisodeResource(1, null, episodeTitle, null, null, 30,
					new[] { new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null) })
			},
			null, null);
}
