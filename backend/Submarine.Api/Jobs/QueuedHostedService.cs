namespace Submarine.Api.Jobs;

public class QueuedHostedService : BackgroundService
{
	private readonly IBackgroundTaskQueue _taskQueue;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<QueuedHostedService> _logger;

	public QueuedHostedService(IBackgroundTaskQueue taskQueue, IServiceScopeFactory scopeFactory,
		ILogger<QueuedHostedService> logger)
	{
		_taskQueue = taskQueue;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			Func<IServiceProvider, CancellationToken, Task> workItem;

			try
			{
				workItem = await _taskQueue.DequeueAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}

			try
			{
				using var scope = _scopeFactory.CreateScope();
				await workItem(scope.ServiceProvider, stoppingToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred executing a queued background task");
			}
		}
	}
}
