using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Commands;
using Submarine.Core.Download;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Library;
using Submarine.Core.MediaFiles;
using Submarine.Core.Parser;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Import;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Polls every enabled download client, reconciles its items with tracked downloads, and drives
///     completed and failed download handling.
/// </summary>
public sealed class DownloadMonitorCommandHandler(
	SubmarineDbContext db,
	IDownloadClientProvider clientProvider,
	IDownloadClientStatusTracker statusTracker,
	IImportService importService,
	IParser<BaseRelease> releaseParser,
	IEventBus eventBus,
	ICommandQueue commandQueue,
	TimeProvider timeProvider,
	ILogger<DownloadMonitorCommandHandler> logger) : ICommandHandler<DownloadMonitorCommand>
{
	private const int MissedPollThreshold = 3;

	/// <inheritdoc />
	public async Task ExecuteAsync(DownloadMonitorCommand command, ICommandContext context, CancellationToken cancellationToken = default)
	{
		var downloadConfig = await db.DownloadConfig.AsNoTracking().SingleAsync(cancellationToken);
		var mappings = await db.RemotePathMappings.AsNoTracking().ToListAsync(cancellationToken);
		var clients = await clientProvider.GetEnabledAsync(cancellationToken: cancellationToken);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var anyChange = false;

		foreach (var client in clients)
		{
			if (!statusTracker.IsAvailable(client.Entity.Id))
			{
				continue;
			}

			IReadOnlyList<DownloadClientItem> items;
			try
			{
				items = await client.Instance.GetItemsAsync(cancellationToken);
				await statusTracker.RecordSuccessAsync(client.Entity.Id, cancellationToken);
			}
			catch (DownloadClientException exception)
			{
				logger.LogWarning(exception, "Failed to query download client {Client}", client.Entity.Name);
				await statusTracker.RecordFailureAsync(client.Entity.Id, exception.Message, cancellationToken);
				continue;
			}

			var itemsByDownloadId = items.ToDictionary(x => x.DownloadId, StringComparer.OrdinalIgnoreCase);
			var tracked = await db.TrackedDownloads.Where(x => x.DownloadClientId == client.Entity.Id).ToListAsync(cancellationToken);

			foreach (var download in tracked)
			{
				if (!itemsByDownloadId.TryGetValue(download.DownloadId, out var item))
				{
					if (download.State is TrackedDownloadState.IMPORTED or TrackedDownloadState.IGNORED)
					{
						continue;
					}

					download.MissedPolls++;
					anyChange = true;
					if (download.MissedPolls < MissedPollThreshold)
					{
						continue;
					}

					db.HistoryEvents.Add(new HistoryEvent
					{
						Type = HistoryEventType.FAILED,
						SeriesId = download.SeriesId,
						EpisodeId = download.EpisodeIds.Count > 0 ? download.EpisodeIds[0] : null,
						MovieId = download.MovieId,
						MediaVersionId = download.MediaVersionId,
						SourceTitle = download.ReleaseTitle ?? download.Title,
						Quality = download.Quality,
						Languages = download.Languages,
						DownloadId = download.DownloadId,
						Data = JsonSerializer.Serialize(new { reason = "Missing from the download client" }),
						Date = now
					});
					db.TrackedDownloads.Remove(download);
					continue;
				}

				download.MissedPolls = 0;
				anyChange |= await ReconcileAsync(download, item, client, downloadConfig, mappings, now, cancellationToken);
			}

			var trackedIds = tracked.Select(x => x.DownloadId).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items.Where(x => !trackedIds.Contains(x.DownloadId)))
			{
				await CreateUnknownAsync(item, client.Entity, now, cancellationToken);
				anyChange = true;
			}
		}

		await db.SaveChangesAsync(cancellationToken);

		if (anyChange)
		{
			await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		}
	}

	private async Task<bool> ReconcileAsync(
		TrackedDownload download,
		DownloadClientItem item,
		EnabledDownloadClient client,
		DownloadConfig downloadConfig,
		IReadOnlyList<RemotePathMapping> mappings,
		DateTime now,
		CancellationToken cancellationToken)
	{
		var previousStatus = download.Status;
		download.Status = MapStatus(item.Status);
		download.Size = item.TotalSize;
		download.SizeLeft = item.RemainingSize;
		download.OutputPath = item.OutputPath is null
			? download.OutputPath
			: RemotePathResolver.ToLocal(mappings, client.Entity.Name, item.OutputPath);
		if (item.Message is not null)
		{
			download.StatusMessages = [item.Message];
		}

		if (download.State is TrackedDownloadState.IMPORTED or TrackedDownloadState.IGNORED)
		{
			return previousStatus != download.Status;
		}

		switch (item.Status)
		{
			case DownloadItemStatus.COMPLETED:
				return await HandleCompletedAsync(download, item, client, downloadConfig, now, cancellationToken);
			case DownloadItemStatus.FAILED:
				return await HandleFailedAsync(download, item, client, downloadConfig, cancellationToken);
			default:
				if (download.State != TrackedDownloadState.DOWNLOADING)
				{
					download.State = TrackedDownloadState.DOWNLOADING;
					return true;
				}

				return previousStatus != download.Status;
		}
	}

	private async Task<bool> HandleCompletedAsync(
		TrackedDownload download,
		DownloadClientItem item,
		EnabledDownloadClient client,
		DownloadConfig downloadConfig,
		DateTime now,
		CancellationToken cancellationToken)
	{
		if (download.State == TrackedDownloadState.DOWNLOADING)
		{
			download.State = TrackedDownloadState.IMPORT_PENDING;
		}

		if (!downloadConfig.EnableCompletedDownloadHandling)
		{
			return true;
		}

		var stalledImporting = download.State == TrackedDownloadState.IMPORTING && now - download.UpdatedAt > TimeSpan.FromMinutes(15);
		if (download.State is TrackedDownloadState.IMPORT_PENDING || stalledImporting)
		{
			download.State = TrackedDownloadState.IMPORTING;
			await db.SaveChangesAsync(cancellationToken);
			await importService.ImportTrackedDownloadAsync(download.Id, cancellationToken);
			await db.Entry(download).ReloadAsync(cancellationToken);
		}

		if (download.State == TrackedDownloadState.IMPORTED
			&& downloadConfig.RemoveCompletedDownloads
			&& item.CanBeRemoved
			&& (item.Protocol != Protocol.BITTORRENT || item.CanMoveFiles))
		{
			try
			{
				await client.Instance.MarkImportedAsync(download.DownloadId, cancellationToken);
				await client.Instance.RemoveAsync(download.DownloadId, deleteData: false, cancellationToken);
				db.TrackedDownloads.Remove(download);
			}
			catch (DownloadClientException exception)
			{
				logger.LogWarning(exception, "Failed to remove completed download {DownloadId} from {Client}", download.DownloadId, client.Entity.Name);
			}
		}

		return true;
	}

	private async Task<bool> HandleFailedAsync(
		TrackedDownload download,
		DownloadClientItem item,
		EnabledDownloadClient client,
		DownloadConfig downloadConfig,
		CancellationToken cancellationToken)
	{
		if (download.State == TrackedDownloadState.FAILED)
		{
			return false;
		}

		download.State = TrackedDownloadState.FAILED;
		var now = timeProvider.GetUtcNow().UtcDateTime;

		await eventBus.PublishAsync(
			new DownloadFailedEvent(download.DownloadId, download.Title, download.SeriesId, download.MovieId, download.EpisodeIds, download.MediaVersionId, item.Message ?? "Download failed"),
			cancellationToken);

		db.HistoryEvents.Add(new HistoryEvent
		{
			Type = HistoryEventType.FAILED,
			SeriesId = download.SeriesId,
			EpisodeId = download.EpisodeIds.Count > 0 ? download.EpisodeIds[0] : null,
			MovieId = download.MovieId,
			MediaVersionId = download.MediaVersionId,
			SourceTitle = download.ReleaseTitle ?? download.Title,
			Quality = download.Quality,
			Languages = download.Languages,
			DownloadId = download.DownloadId,
			Date = now
		});

		if (downloadConfig.EnableFailedDownloadHandling)
		{
			db.BlocklistItems.Add(new BlocklistItem
			{
				ReleaseTitle = download.ReleaseTitle ?? download.Title,
				Protocol = download.Protocol,
				IndexerId = download.IndexerId,
				SeriesId = download.SeriesId,
				MovieId = download.MovieId,
				EpisodeIds = download.EpisodeIds,
				Reason = item.Message ?? "Download failed",
				Size = download.Size,
				Date = now
			});

			if (downloadConfig.RedownloadFailedReleases)
			{
				if (download.SeriesId is not null && download.EpisodeIds.Count > 0)
				{
					await commandQueue.EnqueueAsync(new EpisodeSearchCommand(download.EpisodeIds), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
				}
				else if (download.MovieId is { } movieId)
				{
					await commandQueue.EnqueueAsync(new MovieSearchCommand([movieId]), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
				}
			}
		}

		if (downloadConfig.RemoveFailedDownloads)
		{
			try
			{
				await client.Instance.RemoveAsync(download.DownloadId, deleteData: true, cancellationToken);
			}
			catch (DownloadClientException exception)
			{
				logger.LogWarning(exception, "Failed to remove failed download {DownloadId} from {Client}", download.DownloadId, client.Entity.Name);
			}

			db.TrackedDownloads.Remove(download);
		}

		return true;
	}

	private async Task CreateUnknownAsync(DownloadClientItem item, DownloadClient client, DateTime now, CancellationToken cancellationToken)
	{
		var download = new TrackedDownload
		{
			DownloadClientId = client.Id,
			DownloadId = item.DownloadId,
			Title = item.Title,
			Protocol = item.Protocol,
			Status = MapStatus(item.Status),
			State = item.IsReadOnly ? TrackedDownloadState.IGNORED : TrackedDownloadState.DOWNLOADING,
			OutputPath = item.OutputPath,
			Size = item.TotalSize,
			SizeLeft = item.RemainingSize,
			Added = now
		};

		if (!item.IsReadOnly)
		{
			await TryMatchLibraryAsync(download, item.Title, cancellationToken);
		}

		db.TrackedDownloads.Add(download);
	}

	private async Task TryMatchLibraryAsync(TrackedDownload download, string title, CancellationToken cancellationToken)
	{
		BaseRelease? parsed;
		try
		{
			parsed = releaseParser.Parse(title);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return;
		}

		var cleanTitle = TitleNormalizer.CleanTitle(parsed.Title);
		if (cleanTitle.Length == 0)
		{
			return;
		}

		var series = await db.Series.FirstOrDefaultAsync(x => x.CleanTitle == cleanTitle, cancellationToken);
		if (series is not null)
		{
			download.SeriesId = series.Id;
			download.ReleaseTitle = parsed.FullTitle;
			download.Quality = parsed.Quality;
			download.Languages = [.. parsed.Languages];
			download.ReleaseGroup = parsed.ReleaseGroup;
			return;
		}

		var movie = await db.Movies.FirstOrDefaultAsync(x => x.CleanTitle == cleanTitle, cancellationToken);
		if (movie is not null)
		{
			download.MovieId = movie.Id;
			download.ReleaseTitle = parsed.FullTitle;
			download.Quality = parsed.Quality;
			download.Languages = [.. parsed.Languages];
			download.ReleaseGroup = parsed.ReleaseGroup;
		}
	}

	private static TrackedDownloadStatus MapStatus(DownloadItemStatus status)
		=> status switch
		{
			DownloadItemStatus.QUEUED => TrackedDownloadStatus.QUEUED,
			DownloadItemStatus.DOWNLOADING => TrackedDownloadStatus.DOWNLOADING,
			DownloadItemStatus.PAUSED => TrackedDownloadStatus.PAUSED,
			DownloadItemStatus.COMPLETED => TrackedDownloadStatus.COMPLETED,
			DownloadItemStatus.FAILED => TrackedDownloadStatus.FAILED,
			DownloadItemStatus.WARNING => TrackedDownloadStatus.WARNING,
			_ => TrackedDownloadStatus.WARNING
		};
}
