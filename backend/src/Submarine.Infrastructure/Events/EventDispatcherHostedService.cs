using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Submarine.Core.Events;

namespace Submarine.Infrastructure.Events;

/// <summary>
///     Reads events from the bus and dispatches them to scoped handlers: the handlers registered for the
///     concrete event type plus catch-all handlers registered for <see cref="IDomainEvent" />.
///     Handler exceptions are logged and swallowed so one broken handler cannot block the bus.
/// </summary>
public sealed class EventDispatcherHostedService(
	ChannelEventBus bus,
	IServiceScopeFactory scopeFactory,
	ILogger<EventDispatcherHostedService> logger) : BackgroundService
{
	private static readonly ConcurrentDictionary<Type, (Type ServiceType, MethodInfo Handle)> Dispatchers = new();
	private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var reader = bus.Reader;
		while (await reader.WaitToReadAsync(stoppingToken))
		{
			while (reader.TryRead(out var @event))
			{
				try
				{
					await using var scope = scopeFactory.CreateAsyncScope();
					await DispatchAsync(scope.ServiceProvider, @event, stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Failed to dispatch event {Event}", @event.GetType().Name);
				}
			}
		}
	}

	/// <summary>
	///     Invoke every handler for the event inside the given scope.
	/// </summary>
	public async Task DispatchAsync(IServiceProvider services, IDomainEvent @event, CancellationToken cancellationToken)
	{
		var (serviceType, handle) = Dispatchers.GetOrAdd(@event.GetType(), eventType =>
		{
			var closed = typeof(IEventHandler<>).MakeGenericType(eventType);
			return (closed, closed.GetMethod(nameof(IEventHandler<IDomainEvent>.HandleAsync))!);
		});

		var handlers = services.GetServices(serviceType)
			.Concat(services.GetServices<IEventHandler<IDomainEvent>>())
			.Where(handler => handler is not null)
			.Distinct()
			.ToList();

		foreach (var handler in handlers)
		{
			try
			{
				var task = handler is IEventHandler<IDomainEvent> catchAll
					? catchAll.HandleAsync(@event, cancellationToken)
					: (Task)handle.Invoke(handler, [@event, cancellationToken])!;
				await task.WaitAsync(HandlerTimeout, cancellationToken);
			}
			catch (TimeoutException)
			{
				logger.LogWarning(
					"Event handler {Handler} timed out after {Timeout} for event {Event}, continuing without waiting for it",
					handler!.GetType().Name,
					HandlerTimeout,
					@event.GetType().Name);
			}
			catch (Exception ex)
			{
				logger.LogError(
					ex,
					"Event handler {Handler} failed for event {Event}",
					handler!.GetType().Name,
					@event.GetType().Name);
			}
		}
	}
}
