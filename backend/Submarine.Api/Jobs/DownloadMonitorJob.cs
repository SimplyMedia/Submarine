using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Services;
using Submarine.Core.Config;
using Submarine.Core.Download;
using Submarine.Core.History;

namespace Submarine.Api.Jobs;

/// <summary>
///     Polls enabled download clients, updates tracked downloads and enqueues imports for completed ones
/// </summary>
public sealed class DownloadMonitorJob : IScheduledJob
{
	private static readonly TimeSpan ReimportInterval = TimeSpan.FromMinutes(15);

	// tracks the last import enqueue per download so a completed-but-unimported row is retried, but not every tick;
	// lives on the singleton job instance
	private readonly ConcurrentDictionary<int, DateTimeOffset> _lastImportEnqueue = new();

	public string Name => "DownloadMonitor";

	public TimeSpan Interval => TimeSpan.FromMinutes(1);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var context = scopedProvider.GetRequiredService<SubmarineDatabaseContext>();
		var factory = scopedProvider.GetRequiredService<DownloadClientFactory>();
		var queue = scopedProvider.GetRequiredService<IBackgroundTaskQueue>();
		var settings = scopedProvider.GetRequiredService<SettingsService>();
		var history = scopedProvider.GetRequiredService<HistoryService>();
		var blocklist = scopedProvider.GetRequiredService<BlocklistService>();
		var logger = scopedProvider.GetRequiredService<ILogger<DownloadMonitorJob>>();

		var downloadConfig = await settings.GetDownloadConfigAsync();

		var configs = await context.DownloadClients.AsNoTracking()
			.Where(c => c.Enable)
			.ToListAsync(cancellationToken);

		foreach (var config in configs)
		{
			try
			{
				var client = factory.Create(config);

				var items = (await client.GetItemsAsync(cancellationToken))
					.ToDictionary(i => i.DownloadId);

				var tracked = await context.TrackedDownloads
					.Where(t => t.DownloadClientConfigId == config.Id && !t.Imported)
					.ToListAsync(cancellationToken);

				var now = DateTimeOffset.UtcNow;
				var toImport = new List<int>();
				var toFail = new List<(TrackedDownload Download, DownloadClientItem Item)>();

				foreach (var download in tracked)
				{
					if (!items.TryGetValue(download.DownloadId, out var item))
						continue;

					var justCompleted = item.Status == DownloadItemStatus.COMPLETED
					                    && download.Status != DownloadItemStatus.COMPLETED;
					var justFailed = item.Status == DownloadItemStatus.FAILED
					                 && download.Status != DownloadItemStatus.FAILED;

					// Invariant: a failed download's status is persisted only after HandleFailedAsync succeeds. Handling
					// runs after the batch save and removes the row, so a throw leaves the status unchanged and the
					// failure is retried on the next tick instead of being silently consumed.
					if (downloadConfig.EnableFailedDownloadHandling && justFailed)
					{
						toFail.Add((download, item));
						continue;
					}

					download.Status = item.Status;

					if (item.OutputPath != null)
						download.OutputPath = item.OutputPath;

					// The first enqueue is edge-triggered; a still-completed row whose import never landed is retried
					// after ReimportInterval. ImportService is idempotent, so a redundant enqueue is harmless.
					if (item.Status == DownloadItemStatus.COMPLETED && !download.Imported
					    && (justCompleted || ShouldReimport(download.Id, now)))
					{
						toImport.Add(download.Id);
						_lastImportEnqueue[download.Id] = now;
					}
				}

				await context.SaveChangesAsync(cancellationToken);

				foreach (var id in toImport)
					await queue.QueueAsync((sp, ct) =>
						sp.GetRequiredService<ImportService>().ImportTrackedDownloadAsync(id, ct));

				foreach (var (download, item) in toFail)
					await HandleFailedAsync(context, client, queue, history, blocklist, downloadConfig, config,
						download, item, logger, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Monitoring download client {Client} failed", config.Name);
			}
		}
	}

	private bool ShouldReimport(int downloadId, DateTimeOffset now)
		=> !_lastImportEnqueue.TryGetValue(downloadId, out var last) || now - last >= ReimportInterval;

	private static async Task HandleFailedAsync(SubmarineDatabaseContext context, IDownloadClient client,
		IBackgroundTaskQueue queue, HistoryService history, BlocklistService blocklist, DownloadConfig downloadConfig,
		DownloadClientConfig config, TrackedDownload download, DownloadClientItem item, ILogger logger,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, string> { ["downloadClient"] = config.Name };

		if (download.Indexer != null)
			data["indexer"] = download.Indexer;

		if (item.Message != null)
			data["reason"] = item.Message;

		await history.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.FAILED,
			SeriesId = download.SeriesId,
			MovieId = download.MovieId,
			SourceTitle = download.ReleaseTitle,
			Data = data
		}, cancellationToken);

		await blocklist.AddAsync(new BlocklistItem
		{
			ReleaseTitle = download.ReleaseTitle,
			Protocol = download.Protocol,
			Indexer = download.Indexer,
			SeriesId = download.SeriesId,
			MovieId = download.MovieId,
			Reason = item.Message ?? "download failed"
		});

		if (downloadConfig.RemoveFailedFromClient)
			try
			{
				await client.RemoveItemAsync(download.DownloadId, true, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Removing failed download {Download} from client {Client} failed",
					download.DownloadId, config.Name);
			}

		context.TrackedDownloads.Remove(download);
		await context.SaveChangesAsync(cancellationToken);

		if (downloadConfig.RedownloadFailedReleases)
			await EnqueueRedownloadAsync(context, queue, download, cancellationToken);
	}

	private static async Task EnqueueRedownloadAsync(SubmarineDatabaseContext context, IBackgroundTaskQueue queue,
		TrackedDownload download, CancellationToken cancellationToken)
	{
		if (download.MovieId is { } movieId)
		{
			await queue.QueueAsync((sp, ct) =>
				sp.GetRequiredService<AutomaticSearchService>().SearchAndGrabMovieAsync(movieId, ct));

			return;
		}

		if (download.SeriesId is not { } seriesId || download.EpisodeIds.Count == 0)
			return;

		var episodes = await context.Episodes.AsNoTracking()
			.Where(e => download.EpisodeIds.Contains(e.Id))
			.ToListAsync(cancellationToken);

		var seasons = episodes.Select(e => e.SeasonNumber).Distinct().ToList();

		if (seasons.Count == 1)
		{
			var season = seasons[0];

			var seasonEpisodeCount = await context.Episodes.AsNoTracking()
				.CountAsync(e => e.SeriesId == seriesId && e.SeasonNumber == season, cancellationToken);

			if (episodes.Count == seasonEpisodeCount)
			{
				await queue.QueueAsync((sp, ct) =>
					sp.GetRequiredService<AutomaticSearchService>().SearchAndGrabSeasonAsync(seriesId, season, ct));

				return;
			}
		}

		foreach (var episodeId in download.EpisodeIds)
			await queue.QueueAsync((sp, ct) =>
				sp.GetRequiredService<AutomaticSearchService>().SearchAndGrabEpisodeAsync(episodeId, ct));
	}
}
