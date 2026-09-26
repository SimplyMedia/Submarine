using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Api.Hubs;
using Submarine.Api.Modules;
using Submarine.Core.Modules;
using Submarine.Core.Events;

namespace Submarine.Api.Features.Events;

/// <summary>
///     Realtime events: SignalR hub plus a forwarder from the domain event bus.
/// </summary>
public sealed class EventsModule : IServiceModule, IEndpointModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<IEventHandler<IDomainEvent>, HubEventForwarder>();

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
		=> endpoints.MapHub<EventsHub>("/hubs/events");
}
