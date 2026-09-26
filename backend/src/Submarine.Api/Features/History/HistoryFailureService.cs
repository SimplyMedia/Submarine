using Microsoft.EntityFrameworkCore;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Provider;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.History;

/// <summary>Shared native operation for marking a grabbed release as failed.</summary>
public sealed class HistoryFailureService(
	SubmarineDbContext db,
	IDownloadClientProvider clientProvider,
	ICommandQueue commandQueue,
	IEventBus eventBus,
	TimeProvider timeProvider)
{
	public async Task<HistoryFailureResult> MarkFailedAsync(int id, CancellationToken cancellationToken)
	{
		var grabbed = await db.HistoryEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (grabbed is null)
			return new(null, null);
		if (grabbed.Type != HistoryEventType.GRABBED || grabbed.DownloadId is null)
			return new(null, "Only a grabbed history entry with a download id can be marked as failed");

		var download = await db.TrackedDownloads.FirstOrDefaultAsync(x => x.DownloadId == grabbed.DownloadId, cancellationToken);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var episodeIds = download?.EpisodeIds ?? (grabbed.EpisodeId is { } episodeId ? [episodeId] : []);

		await eventBus.PublishAsync(
			new DownloadFailedEvent(grabbed.DownloadId, grabbed.SourceTitle, grabbed.SeriesId, grabbed.MovieId, episodeIds, grabbed.MediaVersionId, "Marked as failed"),
			cancellationToken);

		var failedEvent = new HistoryEvent
		{
			Type = HistoryEventType.FAILED,
			SeriesId = grabbed.SeriesId,
			EpisodeId = grabbed.EpisodeId,
			MovieId = grabbed.MovieId,
			MediaVersionId = grabbed.MediaVersionId,
			SourceTitle = grabbed.SourceTitle,
			Quality = grabbed.Quality,
			Languages = grabbed.Languages,
			DownloadId = grabbed.DownloadId,
			Date = now
		};
		db.HistoryEvents.Add(failedEvent);

		db.BlocklistItems.Add(new BlocklistItem
		{
			ReleaseTitle = grabbed.SourceTitle,
			Protocol = download?.Protocol ?? Protocol.BITTORRENT,
			SeriesId = grabbed.SeriesId,
			MovieId = grabbed.MovieId,
			EpisodeIds = episodeIds,
			Reason = "Marked as failed",
			Date = now
		});

		if (download is not null)
		{
			var client = await clientProvider.GetAsync(download.DownloadClientId, cancellationToken);
			if (client is not null)
			{
				try
				{
					await client.Instance.RemoveAsync(download.DownloadId, deleteData: true, cancellationToken);
				}
				catch (Submarine.Core.Download.DownloadClientException)
				{
					// Best-effort cleanup mirrors the native operation.
				}
			}

			db.TrackedDownloads.Remove(download);
		}

		if (grabbed.SeriesId is not null && episodeIds.Count > 0)
			await commandQueue.EnqueueAsync(new EpisodeSearchCommand(episodeIds), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);
		else if (grabbed.MovieId is { } movieId)
			await commandQueue.EnqueueAsync(new MovieSearchCommand([movieId]), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);

		await db.SaveChangesAsync(cancellationToken);
		await eventBus.PublishAsync(new QueueUpdatedEvent(), cancellationToken);
		return new(failedEvent, null);
	}
}

public sealed record HistoryFailureResult(HistoryEvent? FailedEvent, string? Error);
