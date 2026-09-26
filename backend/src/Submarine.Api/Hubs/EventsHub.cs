using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Submarine.Core.Events;
using Submarine.Infrastructure.Events;

namespace Submarine.Api.Hubs;

/// <summary>
///     Realtime event stream. Clients subscribe to the "event" message carrying {type, payload}.
/// </summary>
[Authorize]
public sealed class EventsHub : Hub
{
}

/// <summary>
///     Forwards every domain event to connected hub clients.
/// </summary>
public sealed class HubEventForwarder(IHubContext<EventsHub> hubContext) : IEventHandler<IDomainEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
		=> hubContext.Clients.All.SendAsync(
			"event",
			new HubEventMessage(@event.GetType().Name, @event),
			cancellationToken);
}

/// <summary>
///     The message shape sent to hub clients.
/// </summary>
/// <param name="Type">Event type name.</param>
/// <param name="Payload">The event itself.</param>
public sealed record HubEventMessage(string Type, object Payload);
