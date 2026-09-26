using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Indexers;
using Submarine.Core.Modules;
using Submarine.Core.Validator;
using Submarine.Infrastructure.DownloadClients;
using Submarine.Infrastructure.Http;
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

		services.AddMemoryCache();
		services.AddSingleton<IOutboundProxyProvider, OutboundProxyProvider>();

		// Applies the outbound proxy and certificate validation settings to every HttpClient
		// created through IHttpClientFactory (indexers still layer per-indexer overrides on top
		// in IndexerProvider/IndexerHttpClientFactory); the factory recreates the primary handler
		// on its default rotation, so a settings change takes effect without a restart.
		services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(provider =>
		{
			var snapshot = provider.GetRequiredService<IOutboundProxyProvider>().GetSnapshotAsync().GetAwaiter().GetResult();
			var handler = new SocketsHttpHandler
			{
				AutomaticDecompression = System.Net.DecompressionMethods.All,
				SslOptions = new System.Net.Security.SslClientAuthenticationOptions
				{
					RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
						OutboundProxyResolver.ValidateCertificate(snapshot.CertificateValidation, sender, certificate, chain, errors)
				}
			};
			OutboundProxyResolver.ApplyProxy(handler, snapshot);
			return handler;
		}));

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
