using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Extensions;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.Download;
using Submarine.Core.History;

namespace Submarine.Api.Services;

public class QueueService
{
	private readonly SubmarineDatabaseContext _context;
	private readonly DownloadClientFactory _factory;
	private readonly IBackgroundTaskQueue _taskQueue;
	private readonly HistoryService _historyService;
	private readonly ILogger<QueueService> _logger;

	public QueueService(SubmarineDatabaseContext context, DownloadClientFactory factory,
		IBackgroundTaskQueue taskQueue, HistoryService historyService, ILogger<QueueService> logger)
	{
		_context = context;
		_factory = factory;
		_taskQueue = taskQueue;
		_historyService = historyService;
		_logger = logger;
	}

	public async Task<PagedResult<QueueItemResponse>> GetPagedAsync(int page, int pageSize)
	{
		// SQLite can't translate DateTimeOffset ORDER BY; Id is monotonic with creation time
		var paged = await _context.TrackedDownloads.AsNoTracking()
			.Where(t => !t.Imported)
			.OrderByDescending(t => t.Id)
			.ToPagedResultAsync(page, pageSize);

		var items = await LoadClientItemsAsync(paged.Items);

		var mapped = paged.Items
			.Select(t => QueueItemResponse.From(t, items.GetValueOrDefault(t.DownloadId)))
			.ToList();

		return new PagedResult<QueueItemResponse>(mapped, paged.Page, paged.PageSize, paged.TotalItems);
	}

	public async Task DeleteAsync(int id, bool removeFromClient, bool deleteData,
		CancellationToken cancellationToken = default)
	{
		var tracked = await _context.TrackedDownloads.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

		if (tracked == null)
			throw new NotFoundException();

		if (removeFromClient)
		{
			var config = await _context.DownloadClients.AsNoTracking()
				.FirstOrDefaultAsync(c => c.Id == tracked.DownloadClientConfigId, cancellationToken);

			if (config != null)
				await _factory.Create(config).RemoveItemAsync(tracked.DownloadId, deleteData, cancellationToken);
		}

		_context.TrackedDownloads.Remove(tracked);
		await _context.SaveChangesAsync(cancellationToken);

		await _historyService.RecordAsync(new HistoryEvent
		{
			Type = HistoryEventType.DELETED,
			SeriesId = tracked.SeriesId,
			MovieId = tracked.MovieId,
			SourceTitle = tracked.ReleaseTitle,
			Data = new Dictionary<string, string> { ["downloadId"] = tracked.DownloadId }
		}, cancellationToken);
	}

	public async Task ImportAsync(int id, CancellationToken cancellationToken = default)
	{
		if (!await _context.TrackedDownloads.AnyAsync(t => t.Id == id, cancellationToken))
			throw new NotFoundException();

		await _taskQueue.QueueAsync((sp, ct) =>
			sp.GetRequiredService<ImportService>().ImportTrackedDownloadAsync(id, ct));
	}

	private async Task<Dictionary<string, DownloadClientItem>> LoadClientItemsAsync(
		IReadOnlyList<TrackedDownload> tracked)
	{
		var result = new Dictionary<string, DownloadClientItem>();

		var configIds = tracked.Select(t => t.DownloadClientConfigId).Distinct().ToList();

		var configs = await _context.DownloadClients.AsNoTracking()
			.Where(c => configIds.Contains(c.Id))
			.ToListAsync();

		foreach (var config in configs)
		{
			try
			{
				foreach (var item in await _factory.Create(config).GetItemsAsync())
					result[item.DownloadId] = item;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Loading queue items from client {Client} failed", config.Name);
			}
		}

		return result;
	}
}
