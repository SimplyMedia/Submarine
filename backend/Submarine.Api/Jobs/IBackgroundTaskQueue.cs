namespace Submarine.Api.Jobs;

public interface IBackgroundTaskQueue
{
	ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);

	ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
