using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Submarine.Core.Modules;

namespace Submarine.Api.Modules;

/// <summary>
///     A feature's HTTP surface. Discovered by reflection, Program.cs never edits endpoint mappings.
/// </summary>
public interface IEndpointModule
{
	/// <summary>
	///     Map the feature's endpoints.
	/// </summary>
	void Map(IEndpointRouteBuilder endpoints);
}

/// <summary>
///     Discovers and applies modules from the given assemblies.
/// </summary>
public static class ModuleDiscovery
{
	/// <summary>
	///     Find every IServiceModule implementation and run its Register method.
	/// </summary>
	public static IServiceCollection AddModules(
		this IServiceCollection services,
		IConfiguration configuration,
		params System.Reflection.Assembly[] assemblies)
	{
		foreach (var type in FindModules<IServiceModule>(assemblies))
		{
			var module = (IServiceModule)Activator.CreateInstance(type)!;
			module.Register(services, configuration);
			services.TryAddSingleton(type, type);
		}

		return services;
	}

	/// <summary>
	///     Find every IEndpointModule implementation and run its Map method.
	/// </summary>
	public static void MapModules(this IEndpointRouteBuilder endpoints, params System.Reflection.Assembly[] assemblies)
	{
		// Every endpoint can answer with a ProblemDetails for validation, auth, missing rows and conflicts,
		// so the OpenAPI document declares them once here instead of per endpoint.
		var root = endpoints.MapGroup(string.Empty)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		foreach (var type in FindModules<IEndpointModule>(assemblies))
		{
			var module = (IEndpointModule)Activator.CreateInstance(type)!;
			module.Map(root);
		}
	}

	private static IEnumerable<Type> FindModules<TModule>(IEnumerable<System.Reflection.Assembly> assemblies)
		=> assemblies
			.SelectMany(assembly =>
			{
				try
				{
					return assembly.GetTypes();
				}
				catch (System.Reflection.ReflectionTypeLoadException exception)
				{
					return exception.Types.Where(type => type is not null).Select(type => type!);
				}
			})
			.Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(TModule).IsAssignableFrom(type));
}
