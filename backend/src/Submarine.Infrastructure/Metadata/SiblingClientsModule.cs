using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Modules;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Registers the typed clients for the Metadata and Mappings services.
/// </summary>
public sealed class SiblingClientsModule : IServiceModule
{
	/// <inheritdoc />
	public void Register(IServiceCollection services, IConfiguration configuration)
	{
		services.AddHttpClient<IMetadataClient, MetadataClient>(http =>
			{
				http.BaseAddress = new Uri(configuration["Metadata:BaseUrl"] ?? "http://localhost:5100");
			})
			.AddHttpMessageHandler(() => new ApiKeyHandler(configuration["Metadata:ApiKey"]))
			.AddStandardResilienceHandler();

		services.AddHttpClient<IMappingsClient, MappingsClient>(http =>
			{
				http.BaseAddress = new Uri(configuration["Mappings:BaseUrl"] ?? "http://localhost:5200");
			})
			.AddHttpMessageHandler(() => new ApiKeyHandler(configuration["Mappings:ApiKey"]));
	}
}

/// <summary>
///     Attaches the X-Api-Key header to every outgoing request when an api key is configured.
/// </summary>
internal sealed class ApiKeyHandler(string? apiKey) : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrEmpty(apiKey))
		{
			request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
		}

		return base.SendAsync(request, cancellationToken);
	}
}
