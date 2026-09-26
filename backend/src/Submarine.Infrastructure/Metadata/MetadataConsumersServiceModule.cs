using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;
using Submarine.Infrastructure.Metadata.Consumers;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Registers the metadata consumer implementations and the writer that drives them.
/// </summary>
public sealed class MetadataConsumersServiceModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddHttpClient(MetadataConsumerImageDownloader.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(15));

		services.AddSingleton<IMetadataConsumer, KodiMetadataConsumer>();
		services.AddSingleton<IMetadataConsumer, PlexMetadataConsumer>();
		services.AddSingleton<IMetadataConsumer, EmbyMetadataConsumer>();
		services.AddSingleton<IMetadataConsumer, RoksboxMetadataConsumer>();
		services.AddSingleton<IMetadataConsumer, WdtvMetadataConsumer>();
		services.AddSingleton<IMetadataConsumerFactory, MetadataConsumerFactory>();
		services.AddSingleton<IMetadataConsumerImageDownloader, MetadataConsumerImageDownloader>();
		services.AddScoped<IMetadataClientImageResolver, MetadataClientImageResolver>();
		services.AddScoped<IMetadataConsumerWriter, MetadataConsumerWriter>();
	}
}
