using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Registers the command queue, executor, scheduler and all handlers found in the given assemblies.
/// </summary>
public static class CommandsServiceCollectionExtensions
{
	/// <summary>
	///     Add the persisted command queue and scheduler.
	/// </summary>
	/// <param name="services">Service collection.</param>
	/// <param name="handlerAssemblies">Assemblies scanned for ICommandHandler implementations.</param>
	public static IServiceCollection AddSubmarineCommands(this IServiceCollection services, params Assembly[] handlerAssemblies)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<CommandRegistry>();
		services.TryAddSingleton<CommandExecutionRegistry>();
		services.TryAddSingleton<CommandQueue>();
		services.TryAddSingleton<ICommandQueue>(sp => sp.GetRequiredService<CommandQueue>());
		services.TryAddSingleton<ICommandCancellation>(sp => sp.GetRequiredService<CommandQueue>());
		services.AddHostedService<CommandExecutorHostedService>();
		services.AddHostedService<SchedulerHostedService>();

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
					&& interfaceType.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
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
