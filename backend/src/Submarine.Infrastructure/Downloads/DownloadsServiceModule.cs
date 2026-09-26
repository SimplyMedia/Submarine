using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.MediaFiles;
using Submarine.Core.Modules;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Registers the queue, import pipeline and blocklist services.
/// </summary>
public sealed class DownloadsServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddScoped<IBlocklistService, BlocklistService>();
		services.AddScoped<IDownloadClientProvider, DownloadClientProvider>();
		services.AddSingleton<IDownloadClientStatusTracker, DownloadClientStatusTracker>();
	}
}
