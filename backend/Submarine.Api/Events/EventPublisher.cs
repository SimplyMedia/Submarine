using Submarine.Api.Jobs;

namespace Submarine.Api.Events;

public sealed class EventPublisher : IEventPublisher
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IBackgroundTaskQueue _taskQueue;

	public EventPublisher(IServiceProvider serviceProvider, IBackgroundTaskQueue taskQueue)
	{
		_serviceProvider = serviceProvider;
		_taskQueue = taskQueue;
	}

	public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
	{
		var handlers = _serviceProvider.GetServices<IEventHandler<TEvent>>();

		foreach (var handler in handlers)
		{
			var handlerType = handler.GetType();

			await _taskQueue.QueueAsync((sp, ct) =>
				((IEventHandler<TEvent>)sp.GetRequiredService(handlerType)).HandleAsync(@event, ct));
		}
	}
}
