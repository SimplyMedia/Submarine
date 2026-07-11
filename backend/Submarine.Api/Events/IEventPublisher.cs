namespace Submarine.Api.Events;

public interface IEventPublisher
{
	ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}
