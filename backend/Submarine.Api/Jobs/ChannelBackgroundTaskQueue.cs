using System.Threading.Channels;

namespace Submarine.Api.Jobs;

public class ChannelBackgroundTaskQueue : IBackgroundTaskQueue
{
	private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
		Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

	public ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
		=> _channel.Writer.WriteAsync(workItem);

	public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
		=> _channel.Reader.ReadAsync(cancellationToken);
}
