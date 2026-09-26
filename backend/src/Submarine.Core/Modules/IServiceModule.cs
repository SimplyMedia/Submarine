using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Submarine.Core.Modules;

/// <summary>
///     Registers the services of one feature. Implementations are discovered by reflection at startup
///     from the Api and Infrastructure assemblies and need a parameterless constructor.
/// </summary>
public interface IServiceModule
{
	void Register(IServiceCollection services, IConfiguration configuration);
}
