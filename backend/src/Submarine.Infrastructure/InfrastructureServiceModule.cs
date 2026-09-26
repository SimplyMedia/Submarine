using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Indexers;
using Submarine.Core.Modules;
using Submarine.Core.Validator;
using Submarine.Infrastructure.DownloadClients;
using Submarine.Infrastructure.Indexers;
using Submarine.Infrastructure.Indexers.Cardigann;

namespace Submarine.Infrastructure;

/// <summary>
///     Registers the download client and indexer engines and the release validators the parser stack needs.
/// </summary>
public sealed class InfrastructureServiceModule : IServiceModule
{
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<TorrentReleaseValidatorService>();
		services.AddSingleton<UsenetReleaseValidatorService>();

		services.AddSubmarineDownloadClients();

		services.AddSingleton<IndexerHttpClientFactory>();
		services.AddSingleton<IndexerDefinitionLoader>(provider =>
			new IndexerDefinitionLoader(
				provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IndexerDefinitionLoader>>(),
				System.IO.Path.Combine(provider.GetRequiredService<Persistence.DataDirectory>().Path, "definitions")));
		services.AddHttpClient<IndexerDefinitionSyncClient>(client =>
		{
			client.Timeout = TimeSpan.FromSeconds(60);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Submarine/2.0");
		});
		services.AddSingleton<IIndexerFactory, IndexerFactory>();
	}
}
