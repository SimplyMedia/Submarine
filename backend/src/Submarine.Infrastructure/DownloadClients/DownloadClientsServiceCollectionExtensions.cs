using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Download;

namespace Submarine.Infrastructure.DownloadClients;

/// <summary>
///     Dependency injection registration for the download client stack
/// </summary>
public static class DownloadClientsServiceCollectionExtensions
{
	/// <summary>
	///     Registers the shared download client http client and the <see cref="IDownloadClientFactory" />
	/// </summary>
	public static IServiceCollection AddSubmarineDownloadClients(this IServiceCollection services)
	{
		services.AddHttpClient(DownloadClientFactory.HttpClientName,
			client => client.Timeout = DownloadClientFactory.HttpTimeout);
		services.AddSingleton<IDownloadClientFactory, DownloadClientFactory>();

		return services;
	}
}
