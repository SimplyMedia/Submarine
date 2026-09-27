using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.IntegrationTests.Library;

/// <summary>
///     Hosts the real API against a temporary Sqlite database with a stubbed IMetadataClient.
/// </summary>
public sealed class LibraryApiFactory : SubmarineApiFactory
{
	public StubMetadataClient Metadata { get; } = new();

	/// <summary>Optional stub for the shared import list HttpClient. Set before the first client.</summary>
	public HttpMessageHandler? ImportListHttpHandler { get; set; }
	public Submarine.Core.Events.IEventBus? EventBusOverride { get; set; }


	/// <inheritdoc />
	protected override void ConfigureTestServices(IServiceCollection services)
	{
		services.RemoveAll<IMetadataClient>();
		services.AddSingleton<IMetadataClient>(Metadata);
		if (EventBusOverride is { } eventBus)
		{
			services.RemoveAll<Submarine.Core.Events.IEventBus>();
			services.AddSingleton(eventBus);
		}
		if (ImportListHttpHandler is { } handler)
		{
			services.AddHttpClient("SubmarineImportLists")
				.ConfigurePrimaryHttpMessageHandler(() => handler);
		}
	}
}

/// <summary>Static JSON responder for the import list HTTP stub.</summary>
public sealed class StubHttpHandler(string json) : HttpMessageHandler
{
	/// <inheritdoc />
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		=> Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		});
}

/// <summary>Always-failing responder for the import list HTTP stub, to simulate a fetch failure.</summary>
public sealed class FailingHttpHandler : HttpMessageHandler
{
	/// <inheritdoc />
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		=> Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
}
