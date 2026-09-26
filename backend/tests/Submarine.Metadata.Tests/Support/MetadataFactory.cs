using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Metadata.Upstream;

namespace Submarine.Metadata.Tests.Support;

/// <summary>
///     Boots the real Metadata app with both upstream named clients pointed
///     at a stub handler.
/// </summary>
public sealed class MetadataFactory(StubHttpMessageHandler handler, IReadOnlyDictionary<string, string?>? settings = null)
	: WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");
		if (settings is not null)
			foreach (var (key, value) in settings)
				builder.UseSetting(key, value);
		builder.ConfigureServices(services =>
		{
			services.AddHttpClient(TmdbClient.ClientName, client => client.BaseAddress = new Uri("https://tmdb.test/3/"))
				.ConfigurePrimaryHttpMessageHandler(() => handler);
			services.AddHttpClient(TvdbClient.ClientName, client => client.BaseAddress = new Uri("https://tvdb.test/v4/"))
				.ConfigurePrimaryHttpMessageHandler(() => handler);
		});
	}
}
