namespace Submarine.Api.Jobs;

public interface IScheduledJob
{
	string Name { get; }

	TimeSpan Interval { get; }

	Task ExecuteAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken);
}
