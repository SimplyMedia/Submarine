using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.DecisionEngine;
using Submarine.Core.DecisionEngine.CustomFormats;
using Submarine.Core.DecisionEngine.Filter;
using Submarine.Core.Download;
using Submarine.Core.History;
using Submarine.Core.Indexer;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Profile;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Xunit;

namespace Submarine.Api.Tests;

public class RssSyncJobTest : DatabaseTestBase
{
	[Fact]
	public async Task ExecuteAsync_ShouldGrabRelease_WhenFeedItemMatchesMonitoredSeries()
	{
		var (series, episode, version) = await SeedSeriesAsync();
		var torznab = FeedWith(Release());

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		var tracked = await Context.TrackedDownloads.AsNoTracking().SingleAsync();
		Assert.Equal("Show S01E05 1080p WEB-DL x264-GROUP", tracked.ReleaseTitle);
		Assert.Equal(series.Id, tracked.SeriesId);
		Assert.Equal(version.Id, tracked.MediaVersionId);
		Assert.Contains(episode.Id, tracked.EpisodeIds);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldSkipRelease_WhenGuidIsBlocklisted()
	{
		await SeedSeriesAsync();
		Context.Blocklist.Add(new BlocklistItem { Guid = "guid-1", ReleaseTitle = "blocked", Reason = "test" });
		await Context.SaveChangesAsync();

		var torznab = FeedWith(Release());

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldSkipRelease_WhenYoungerThanMinimumAgeOnUsenet()
	{
		await SeedSeriesAsync(usenet: true);
		await SetConfigAsync(new IndexerConfig { Id = 1, RssSyncIntervalMinutes = 30, MinimumAgeMinutes = 1000 });

		var torznab = FeedWith(Release(Protocol.USENET) with { PublishDate = DateTimeOffset.UtcNow });

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldSkipRelease_WhenOlderThanRetention()
	{
		await SeedSeriesAsync();
		await SetConfigAsync(new IndexerConfig { Id = 1, RssSyncIntervalMinutes = 30, RetentionDays = 1 });

		var torznab = FeedWith(Release() with { PublishDate = DateTimeOffset.UtcNow.AddDays(-10) });

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldSkipRelease_WhenLargerThanMaximumSize()
	{
		await SeedSeriesAsync();
		await SetConfigAsync(new IndexerConfig { Id = 1, RssSyncIntervalMinutes = 30, MaximumSizeMb = 1 });

		var torznab = FeedWith(Release() with { Size = 5L * 1024 * 1024 * 1024 });

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldDoNothing_WhenRssSyncDisabled()
	{
		await SeedSeriesAsync();
		await SetConfigAsync(new IndexerConfig { Id = 1, RssSyncIntervalMinutes = 0 });

		var torznab = FeedWith(Release());

		await new RssSyncJob().ExecuteAsync(BuildProvider(torznab, out _), CancellationToken.None);

		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	private static ReleaseInfo Release(Protocol protocol = Protocol.BITTORRENT)
		=> new()
		{
			Title = "Show S01E05 1080p WEB-DL x264-GROUP", Guid = "guid-1",
			DownloadUrl = "http://indexer/download", Protocol = protocol
		};

	private static FakeTorznabSearchClient FeedWith(ReleaseInfo release)
		=> new() { Result = new[] { release } };

	private async Task<(Series Series, Episode Episode, MediaVersion Version)> SeedSeriesAsync(bool usenet = false)
	{
		Context.QualityProfiles.Add(new QualityProfile
		{
			Id = 1, Name = "1080p", Cutoff = 0,
			Items = new List<QualityProfileItem>
			{
				new()
				{
					Quality = new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
					Allowed = true
				}
			}
		});
		Context.LanguageProfiles.Add(new LanguageProfile
		{
			Id = 1, Name = "English", Languages = new List<Language> { Language.ENGLISH }, Cutoff = Language.ENGLISH
		});

		if (usenet)
			Context.Providers.Add(new NewznabIndexer
			{
				Name = "Usenet", Url = "http://indexer/", ApiKey = "key", Mode = ProviderMode.RSS,
				Tags = new List<string>(), Categories = new List<int> { 5000 }
			});
		else
			Context.Providers.Add(new TorznabIndexer
			{
				Name = "Indexer", Url = "http://indexer/", ApiKey = "key", Mode = ProviderMode.RSS,
				Tags = new List<string>(), Categories = new List<int> { 5000 }, AnimeCategories = new List<int>()
			});

		Context.DownloadClients.Add(new DownloadClientConfig
		{
			Name = "client", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 1, SettingsJson = "{}"
		});

		var series = new Series { TvdbId = 42, Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var version = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1,
			Monitored = true
		};
		Context.Versions.Add(version);
		var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Monitored = true };
		Context.Episodes.Add(episode);
		await Context.SaveChangesAsync();

		return (series, episode, version);
	}

	private async Task SetConfigAsync(IndexerConfig config)
	{
		Context.IndexerConfigs.Add(config);
		await Context.SaveChangesAsync();
	}

	private IServiceProvider BuildProvider(FakeTorznabSearchClient torznab, out FakeDownloadClient client)
	{
		client = new FakeDownloadClient { Protocol = Protocol.BITTORRENT };

		var services = new ServiceCollection();

		services.AddSingleton<SubmarineDatabaseContext>(Context);
		services.AddSingleton(Settings());
		services.AddSingleton<ITorznabSearchClient>(torznab);
		services.AddSingleton(ReleaseParser());
		services.AddSingleton(new MediaContextFactory(new SeriesRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			new ReleaseFilterRepository(Context), new CustomFormatRepository(Context)));
		services.AddSingleton(new DownloadDecisionService(NullLogger<DownloadDecisionService>.Instance,
			new FilterEvaluator(), new CustomFormatEvaluator(NullLogger<CustomFormatEvaluator>.Instance)));
		services.AddSingleton(new GrabService(Context, new FakeDownloadClientFactory(client), ReleaseParser(),
			new HistoryService(Context), new FakeEventPublisher(), new ProviderRepository(Context)));
		services.AddSingleton(new BlocklistService(new BlocklistRepository(Context)));
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		return services.BuildServiceProvider();
	}
}
