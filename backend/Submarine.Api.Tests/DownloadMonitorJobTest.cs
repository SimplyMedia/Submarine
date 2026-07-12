using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Repository;
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

public class DownloadMonitorJobTest : DatabaseTestBase
{
	[Fact]
	public async Task ExecuteAsync_ShouldRecordFailedHistoryAndBlocklist_WhenDownloadTransitionsToFailed()
	{
		var (config, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = true, RedownloadFailedReleases = false, RemoveFailedFromClient = true
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");

		await new DownloadMonitorJob().ExecuteAsync(BuildProvider(client, out _), CancellationToken.None);

		var history = await Context.History.AsNoTracking().SingleAsync();
		Assert.Equal(HistoryEventType.FAILED, history.Type);
		Assert.Equal(tracked.ReleaseTitle, history.SourceTitle);
		Assert.Equal(tracked.SeriesId, history.SeriesId);
		Assert.Equal(config.Name, history.Data["downloadClient"]);
		Assert.Equal("Indexer", history.Data["indexer"]);
		Assert.Equal("disk full", history.Data["reason"]);

		var blocked = await Context.Blocklist.AsNoTracking().SingleAsync();
		Assert.Equal(tracked.ReleaseTitle, blocked.ReleaseTitle);
		Assert.Equal("disk full", blocked.Reason);
		Assert.Equal(tracked.SeriesId, blocked.SeriesId);

		Assert.Equal((tracked.DownloadId, true), Assert.Single(client.Removed));
		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotRemoveFromClient_WhenRemoveFailedFromClientDisabled()
	{
		var (_, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = true, RedownloadFailedReleases = false, RemoveFailedFromClient = false
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");

		await new DownloadMonitorJob().ExecuteAsync(BuildProvider(client, out _), CancellationToken.None);

		Assert.Empty(client.Removed);
		Assert.Single(await Context.History.AsNoTracking().ToListAsync());
		Assert.Single(await Context.Blocklist.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldEnqueueAutomaticSearch_WhenRedownloadEnabled()
	{
		var (_, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = true, RedownloadFailedReleases = true, RemoveFailedFromClient = false
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");

		await new DownloadMonitorJob().ExecuteAsync(BuildProvider(client, out var queue), CancellationToken.None);

		Assert.Single(queue.Items);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldDoNothing_WhenFailedHandlingDisabled()
	{
		var (_, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = false, RedownloadFailedReleases = true, RemoveFailedFromClient = true
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");

		await new DownloadMonitorJob().ExecuteAsync(BuildProvider(client, out var queue), CancellationToken.None);

		Assert.Empty(await Context.History.AsNoTracking().ToListAsync());
		Assert.Empty(await Context.Blocklist.AsNoTracking().ToListAsync());
		Assert.Empty(queue.Items);
		Assert.Empty(client.Removed);
		Assert.Single(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotDuplicate_WhenPolledTwiceWithStillFailedStatus()
	{
		var (_, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = true, RedownloadFailedReleases = false, RemoveFailedFromClient = false
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");
		var provider = BuildProvider(client, out _);

		await new DownloadMonitorJob().ExecuteAsync(provider, CancellationToken.None);
		await new DownloadMonitorJob().ExecuteAsync(provider, CancellationToken.None);

		Assert.Single(await Context.History.AsNoTracking().ToListAsync());
		Assert.Single(await Context.Blocklist.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRetryFailedHandling_WhenHandlerThrowsOnFirstPoll()
	{
		var (_, tracked, _) = await SeedFailingDownloadAsync(new DownloadConfig
		{
			Id = 1, EnableFailedDownloadHandling = true, RedownloadFailedReleases = false, RemoveFailedFromClient = false
		});

		var client = ClientWithFailedItem(tracked.DownloadId, "disk full");
		var provider = BuildProvider(client, out _, new ThrowOnceHistoryService(Context));

		await new DownloadMonitorJob().ExecuteAsync(provider, CancellationToken.None);

		// first poll threw mid-handling: nothing persisted, status unchanged so the failure is retried
		Assert.Empty(await Context.Blocklist.AsNoTracking().ToListAsync());
		Assert.Single(await Context.TrackedDownloads.AsNoTracking().ToListAsync());

		await new DownloadMonitorJob().ExecuteAsync(provider, CancellationToken.None);

		Assert.Single(await Context.Blocklist.AsNoTracking().ToListAsync());
		Assert.Empty(await Context.TrackedDownloads.AsNoTracking().ToListAsync());
	}

	[Fact]
	public async Task ExecuteAsync_ShouldReenqueueImport_WhenCompletedButNotImported()
	{
		Context.DownloadConfigs.Add(new DownloadConfig { Id = 1, EnableFailedDownloadHandling = true });

		var config = new DownloadClientConfig
		{
			Name = "client", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 1, SettingsJson = "{}"
		};
		Context.DownloadClients.Add(config);
		await Context.SaveChangesAsync();

		var tracked = new TrackedDownload
		{
			DownloadClientConfigId = config.Id, DownloadId = "abc", Title = "Show S01E05",
			Protocol = Protocol.BITTORRENT, Status = DownloadItemStatus.COMPLETED, Imported = false,
			ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Languages = new List<Language> { Language.ENGLISH }, Indexer = "Indexer", EpisodeIds = new List<int>()
		};
		Context.TrackedDownloads.Add(tracked);
		await Context.SaveChangesAsync();

		var client = new FakeDownloadClient
		{
			Items = new[]
			{
				new DownloadClientItem
				{
					DownloadId = "abc", Title = "Show S01E05", Status = DownloadItemStatus.COMPLETED
				}
			}
		};
		var provider = BuildProvider(client, out var queue);
		var job = new DownloadMonitorJob();

		await job.ExecuteAsync(provider, CancellationToken.None);
		Assert.Single(queue.Items);

		// same instance within the re-enqueue window must not enqueue again
		await job.ExecuteAsync(provider, CancellationToken.None);
		Assert.Single(queue.Items);
	}

	private sealed class ThrowOnceHistoryService : HistoryService
	{
		private int _calls;

		public ThrowOnceHistoryService(SubmarineDatabaseContext context) : base(context)
		{
		}

		public override Task RecordAsync(HistoryEvent @event, CancellationToken cancellationToken = default)
			=> ++_calls == 1
				? throw new InvalidOperationException("history unavailable")
				: base.RecordAsync(@event, cancellationToken);
	}

	private static FakeDownloadClient ClientWithFailedItem(string downloadId, string message)
		=> new()
		{
			Items = new[]
			{
				new DownloadClientItem
				{
					DownloadId = downloadId, Title = "Show S01E05", Status = DownloadItemStatus.FAILED,
					Message = message
				}
			}
		};

	private async Task<(DownloadClientConfig Config, TrackedDownload Tracked, Episode Episode)> SeedFailingDownloadAsync(
		DownloadConfig downloadConfig)
	{
		Context.DownloadConfigs.Add(downloadConfig);

		var config = new DownloadClientConfig
		{
			Name = "client", Type = DownloadClientType.QBITTORRENT, Enable = true, Priority = 1, SettingsJson = "{}"
		};
		Context.DownloadClients.Add(config);

		var series = new Series { TvdbId = 42, Title = "Show", Monitored = true, Type = SeriesType.STANDARD };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var episode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 5, Monitored = true };
		var otherEpisode = new Episode { SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 6, Monitored = true };
		Context.Episodes.AddRange(episode, otherEpisode);
		await Context.SaveChangesAsync();

		var tracked = new TrackedDownload
		{
			DownloadClientConfigId = config.Id,
			DownloadId = "abc",
			Title = "Show S01E05",
			Protocol = Protocol.BITTORRENT,
			Status = DownloadItemStatus.DOWNLOADING,
			ReleaseTitle = "Show S01E05 1080p WEB-DL x264-GROUP",
			Quality = new QualityModel(new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Languages = new List<Language> { Language.ENGLISH },
			Indexer = "Indexer",
			SeriesId = series.Id,
			EpisodeIds = new List<int> { episode.Id }
		};
		Context.TrackedDownloads.Add(tracked);
		await Context.SaveChangesAsync();

		return (config, tracked, episode);
	}

	private IServiceProvider BuildProvider(FakeDownloadClient client, out FakeBackgroundTaskQueue queue,
		HistoryService? history = null)
	{
		queue = new FakeBackgroundTaskQueue();

		var services = new ServiceCollection();

		services.AddSingleton<SubmarineDatabaseContext>(Context);
		services.AddSingleton<DownloadClientFactory>(new FakeDownloadClientFactory(client));
		services.AddSingleton<IBackgroundTaskQueue>(queue);
		services.AddSingleton(Settings());
		services.AddSingleton(history ?? new HistoryService(Context));
		services.AddSingleton(new BlocklistService(new BlocklistRepository(Context)));
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		return services.BuildServiceProvider();
	}
}
