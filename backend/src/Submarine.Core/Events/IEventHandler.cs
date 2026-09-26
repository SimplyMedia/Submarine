namespace Submarine.Core.Events;

/// <summary>
///     Handles domain events of a specific type. Implement <see cref="IEventHandler{IDomainEvent}" />
///     to receive every event.
/// </summary>
/// <typeparam name="TEvent">The event type handled.</typeparam>
public interface IEventHandler<in TEvent>
	where TEvent : IDomainEvent
{
	/// <summary>
	///     Handle the event. Exceptions are logged and swallowed by the dispatcher.
	/// </summary>
	/// <param name="event">The event.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
