using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     Registers the import pipeline: file linking, recycle bin and the orchestrator.
/// </summary>
public sealed class ImportServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IFileLinker, FileLinker>();
		services.AddSingleton<IRecycleBinService, RecycleBinService>();
		services.AddSingleton<IFileDateService, FileDateService>();
		services.AddScoped<IImportService, ImportService>();
		services.AddScoped<ILibraryImportService, LibraryImportService>();
		services.AddScoped<MovieFileOperationService>();
	}
}
