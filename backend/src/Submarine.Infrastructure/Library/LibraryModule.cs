using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.Library;

/// <summary>
///     Registers the library services.
/// </summary>
public sealed class LibraryModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddScoped<LibraryAdder>();
		services.AddScoped<LibraryMutator>();
		services.AddScoped<LibraryRefresher>();
		services.AddScoped<CollectionSyncService>();
		services.AddScoped<CollectionAddMissingService>();
		services.AddScoped<VersionMonitoringService>();
		services.AddScoped<SelectedMovieDeletionService>();
	}
}
