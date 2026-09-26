using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Submarine.Core.Events;

namespace Submarine.Infrastructure.Events;

/// <summary>
///     Registers the event bus, the dispatcher and all handlers found in the given assemblies.
/// </summary>
public static class EventsServiceCollectionExtensions
{
	/// <summary>
	///     Add the in-process event bus.
	/// </summary>
	/// <param name="services">Service collection.</param>
	/// <param name="handlerAssemblies">Assemblies scanned for IEventHandler implementations.</param>
	public static IServiceCollection AddSubmarineEvents(this IServiceCollection services, params Assembly[] handlerAssemblies)
	{
		services.TryAddSingleton<ChannelEventBus>();
		services.TryAddSingleton<IEventBus>(sp => sp.GetRequiredService<ChannelEventBus>());
		services.AddHostedService<EventDispatcherHostedService>();

		foreach (var (serviceType, implementationType) in FindHandlers(handlerAssemblies))
		{
			services.AddScoped(serviceType, implementationType);
		}

		return services;
	}

	private static IEnumerable<(Type ServiceType, Type ImplementationType)> FindHandlers(IEnumerable<Assembly> assemblies)
		=> assemblies
			.SelectMany(GetLoadableTypes)
			.Where(type => type is { IsAbstract: false, IsInterface: false })
			.SelectMany(type => type.GetInterfaces()
				.Where(interfaceType => interfaceType.IsGenericType
					&& interfaceType.GetGenericTypeDefinition() == typeof(IEventHandler<>))
				.Select(interfaceType => (ServiceType: interfaceType, ImplementationType: type)));

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.Where(type => type is not null).Select(type => type!);
		}
	}
}
