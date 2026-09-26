using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Submarine.Metadata.Options;
using Submarine.Metadata.Upstream;

namespace Submarine.Metadata.Tests.Support;

/// <summary>
///     Builds the metadata stack over a stub handler with real HybridCache
///     and a fake clock pinned to 2026-09-26 unless overridden.
/// </summary>
public sealed class MetadataTestHost : IDisposable
{
	private readonly ServiceProvider _provider;

	private MetadataTestHost(ServiceProvider provider) => _provider = provider;

	public MetadataService Service => _provider.GetRequiredService<MetadataService>();

	public TmdbClient Tmdb => _provider.GetRequiredService<TmdbClient>();

	public TvdbClient Tvdb => _provider.GetRequiredService<TvdbClient>();

	public static MetadataTestHost Create(StubHttpMessageHandler handler, DateTimeOffset? now = null)
	{
		var services = new ServiceCollection();
		services.AddHttpClient(TmdbClient.ClientName, client => client.BaseAddress = new Uri("https://tmdb.test/3/"))
			.ConfigurePrimaryHttpMessageHandler(() => handler);
		services.AddHttpClient(TvdbClient.ClientName, client => client.BaseAddress = new Uri("https://tvdb.test/v4/"))
			.ConfigurePrimaryHttpMessageHandler(() => handler);
		services.AddHybridCache();
		services.AddSingleton<TimeProvider>(new FakeTimeProvider(now ?? new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)));
		services.AddOptions<TmdbOptions>();
		services.AddOptions<TvdbOptions>().Configure(o => o.ApiKey = "tvdb-key");
		services.AddOptions<CacheOptions>();
		services.AddSingleton<TmdbClient>();
		services.AddSingleton<TvdbClient>();
		services.AddSingleton<MetadataService>();
		return new MetadataTestHost(services.BuildServiceProvider());
	}

	public void Dispose() => _provider.Dispose();
}
