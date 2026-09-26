namespace Submarine.Core.Events;

/// <summary>
///     In-process publish side of the domain event bus.
/// </summary>
public interface IEventBus
{
	/// <summary>
	///     Publish an event to all registered handlers.
	/// </summary>
	/// <param name="event">The event.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	ValueTask PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
}
