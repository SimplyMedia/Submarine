namespace Submarine.Api.Jobs;

public sealed class SchedulerHostedService : BackgroundService
{
	private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

	private readonly ScheduledJobRunner _runner;
	private readonly IScheduledJobRegistry _registry;
	private readonly IBackgroundTaskQueue _taskQueue;

	public SchedulerHostedService(ScheduledJobRunner runner, IScheduledJobRegistry registry,
		IBackgroundTaskQueue taskQueue)
	{
		_runner = runner;
		_registry = registry;
		_taskQueue = taskQueue;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TickInterval);

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			var now = DateTimeOffset.UtcNow;

			foreach (var job in _runner.Jobs)
			{
				var status = _registry.Get(job.Name);

				if (status is null || status.Running || now < status.NextRun)
					continue;

				if (!_runner.TryStart(job.Name))
					continue;

				await _taskQueue.QueueAsync((sp, ct) => _runner.RunAsync(job.Name, sp, ct));
			}
		}
	}
}
