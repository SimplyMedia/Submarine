namespace Submarine.Api.Jobs;

public sealed class ScheduledJobRunner
{
	private readonly IReadOnlyDictionary<string, IScheduledJob> _jobs;
	private readonly ScheduledJobRegistry _registry;
	private readonly ILogger<ScheduledJobRunner> _logger;

	public ScheduledJobRunner(IEnumerable<IScheduledJob> jobs, ScheduledJobRegistry registry,
		ILogger<ScheduledJobRunner> logger)
	{
		_jobs = jobs.ToDictionary(j => j.Name);
		_registry = registry;
		_logger = logger;

		var now = DateTimeOffset.UtcNow;

		foreach (var job in _jobs.Values)
			_registry.Register(job.Name, job.Interval, now + job.Interval);
	}

	public IEnumerable<IScheduledJob> Jobs
		=> _jobs.Values;

	public bool Contains(string name)
		=> _jobs.ContainsKey(name);

	public bool TryStart(string name)
		=> _registry.TryMarkRunning(name);

	public async Task RunAsync(string name, IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		if (!_jobs.TryGetValue(name, out var job))
			return;

		try
		{
			await job.ExecuteAsync(scopedProvider, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Scheduled job {JobName} failed", name);
		}
		finally
		{
			_registry.MarkCompleted(name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + job.Interval);
		}
	}
}
