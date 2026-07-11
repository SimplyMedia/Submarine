namespace Submarine.Api.Events;

public interface IEventHandler<in TEvent>
{
	Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
