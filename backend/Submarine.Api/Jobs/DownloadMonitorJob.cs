using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Api.Services;
using Submarine.Core.Download;

namespace Submarine.Api.Jobs;

/// <summary>
///     Polls enabled download clients, updates tracked downloads and enqueues imports for completed ones
/// </summary>
public sealed class DownloadMonitorJob : IScheduledJob
{
	public string Name => "DownloadMonitor";

	public TimeSpan Interval => TimeSpan.FromMinutes(1);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var context = scopedProvider.GetRequiredService<SubmarineDatabaseContext>();
		var factory = scopedProvider.GetRequiredService<DownloadClientFactory>();
		var queue = scopedProvider.GetRequiredService<IBackgroundTaskQueue>();
		var logger = scopedProvider.GetRequiredService<ILogger<DownloadMonitorJob>>();

		var configs = await context.DownloadClients.AsNoTracking()
			.Where(c => c.Enable)
			.ToListAsync(cancellationToken);

		foreach (var config in configs)
		{
			try
			{
				var items = (await factory.Create(config).GetItemsAsync(cancellationToken))
					.ToDictionary(i => i.DownloadId);

				var tracked = await context.TrackedDownloads
					.Where(t => t.DownloadClientConfigId == config.Id && !t.Imported)
					.ToListAsync(cancellationToken);

				var toImport = new List<int>();

				foreach (var download in tracked)
				{
					if (!items.TryGetValue(download.DownloadId, out var item))
						continue;

					var justCompleted = item.Status == DownloadItemStatus.COMPLETED
					                    && download.Status != DownloadItemStatus.COMPLETED;

					download.Status = item.Status;

					if (item.OutputPath != null)
						download.OutputPath = item.OutputPath;

					if (justCompleted)
						toImport.Add(download.Id);
				}

				await context.SaveChangesAsync(cancellationToken);

				foreach (var id in toImport)
					await queue.QueueAsync((sp, ct) =>
						sp.GetRequiredService<ImportService>().ImportTrackedDownloadAsync(id, ct));
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Monitoring download client {Client} failed", config.Name);
			}
		}
	}
}
