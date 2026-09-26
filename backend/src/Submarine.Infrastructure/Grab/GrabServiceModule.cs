using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.Grab;

/// <summary>
///     Registers the grab service.
/// </summary>
public sealed class GrabServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
		=> services.AddScoped<IGrabService, GrabService>();
}
