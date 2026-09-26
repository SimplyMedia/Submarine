using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Registers the import list implementations.
/// </summary>
public sealed class ImportListsModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddHttpClient("SubmarineImportLists");
		services.AddScoped<IImportList, TmdbImportList>();
		services.AddScoped<IImportList, TraktImportList>();
		services.AddScoped<IImportList, AniListImportList>();
		services.AddScoped<IImportList, PlexImportList>();
		services.AddScoped<IImportList, InstanceImportList>();
		services.AddScoped<IImportList, JsonFeedImportList>();
	}
}
