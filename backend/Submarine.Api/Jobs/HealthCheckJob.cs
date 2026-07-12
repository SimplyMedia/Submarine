using Submarine.Api.Events;
using Submarine.Api.Services;

namespace Submarine.Api.Jobs;

/// <summary>
///     Periodically runs the health checks and publishes a <see cref="HealthIssueEvent" /> for each new issue
/// </summary>
public sealed class HealthCheckJob : IScheduledJob
{
	private HashSet<string> _previousIssues = new();

	public string Name => "HealthCheck";

	public TimeSpan Interval => TimeSpan.FromMinutes(30);

	public async Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		var issues = await scopedProvider.GetRequiredService<HealthService>().CheckAsync(cancellationToken);
		var eventPublisher = scopedProvider.GetRequiredService<IEventPublisher>();
		var logger = scopedProvider.GetRequiredService<ILogger<HealthCheckJob>>();

		var current = new HashSet<string>();

		foreach (var issue in issues)
		{
			var key = $"{issue.Source}:{issue.Message}";

			if (current.Add(key) && !_previousIssues.Contains(key))
				await eventPublisher.PublishAsync(new HealthIssueEvent(issue.Type, issue.Source, issue.Message),
					cancellationToken);
		}

		foreach (var resolved in _previousIssues.Except(current))
			logger.LogDebug("Health issue resolved: {Issue}", resolved);

		_previousIssues = current;
	}
}
