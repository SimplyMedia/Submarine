using System.Net;
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
			.AddHttpMessageHandler(() => new SiblingResponseHandler("Metadata service"))
			.AddStandardResilienceHandler();

		services.AddHttpClient<IMappingsClient, MappingsClient>(http =>
			{
				http.BaseAddress = new Uri(configuration["Mappings:BaseUrl"] ?? "http://localhost:5200");
			})
			.AddHttpMessageHandler(() => new ApiKeyHandler(configuration["Mappings:ApiKey"]))
			.AddHttpMessageHandler(() => new SiblingResponseHandler("Mappings service"));
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

/// <summary>
///     Wraps transport failures and non-2xx/404 responses (after any inner resilience retries) into a
///     <see cref="SiblingServiceException" /> naming the service, so the API reports a clean 502 instead
///     of a generic 500 for a plain <see cref="HttpRequestException" />. 404 passes through unchanged:
///     callers use it to mean "not found", not a service failure.
/// </summary>
internal sealed class SiblingResponseHandler(string serviceName) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response;
		try
		{
			response = await base.SendAsync(request, cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new SiblingServiceException(serviceName, $"{serviceName} is unreachable: {ex.Message}", ex);
		}

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return response;
		}

		try
		{
			response.EnsureSuccessStatusCode();
		}
		catch (HttpRequestException ex)
		{
			response.Dispose();
			throw new SiblingServiceException(serviceName, $"{serviceName} returned {(int)ex.StatusCode.GetValueOrDefault(HttpStatusCode.BadGateway)}", ex);
		}

		return response;
	}
}
