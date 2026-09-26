using Submarine.Core.Download;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Queue;

/// <summary>Applies the native queue removal policy to a tracked download.</summary>
public sealed class QueueRemovalService(
	SubmarineDbContext db,
	IDownloadClientProvider clientProvider,
	ICommandQueue commandQueue,
	TimeProvider timeProvider)
{
	public async Task RemoveAsync(TrackedDownload download, QueueDeleteOptions options, CancellationToken cancellationToken)
	{
		if (options.RemoveFromClient ?? false)
		{
			var client = await clientProvider.GetAsync(download.DownloadClientId, cancellationToken);
			if (client is not null)
			{
				try
				{
					await client.Instance.RemoveAsync(download.DownloadId, deleteData: true, cancellationToken);
				}
				catch (DownloadClientException)
				{
					// Best effort; the row is still removed below.
				}
			}
		}

		if (options.Blocklist ?? false)
		{
			db.BlocklistItems.Add(new BlocklistItem
			{
				ReleaseTitle = download.ReleaseTitle ?? download.Title,
				Protocol = download.Protocol,
				IndexerId = download.IndexerId,
				SeriesId = download.SeriesId,
				MovieId = download.MovieId,
				EpisodeIds = download.EpisodeIds,
				Reason = "Removed from queue",
				Size = download.Size,
				Date = timeProvider.GetUtcNow().UtcDateTime
			});
		}

		if (!(options.SkipRedownload ?? false))
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

		db.TrackedDownloads.Remove(download);
	}
}
