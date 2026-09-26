using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Http;

/// <summary>
///     Immutable snapshot of the outbound proxy and certificate validation configuration.
/// </summary>
public sealed record OutboundProxySnapshot(
	bool Enabled,
	IndexerProxyType Type,
	string Host,
	int Port,
	string? Username,
	string? Password,
	string BypassFilter,
	bool BypassLocalAddresses,
	CertificateValidationType CertificateValidation);

/// <summary>
///     Provides the current outbound proxy configuration with a short cache, read by every
///     process-wide HttpClient primary handler factory.
/// </summary>
public interface IOutboundProxyProvider
{
	/// <summary>
	///     Gets the current snapshot, cached for a few seconds.
	/// </summary>
	Task<OutboundProxySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Drops the cache so changes apply to handlers created after this call.
	/// </summary>
	void Invalidate();
}

/// <summary>
///     Default provider reading the GeneralConfig singleton with a 30 second cache.
/// </summary>
public sealed class OutboundProxyProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory) : IOutboundProxyProvider
{
	private const string CacheKey = "submarine.outbound-proxy.snapshot";

	/// <inheritdoc />
	public async Task<OutboundProxySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out OutboundProxySnapshot? cached) && cached is not null)
		{
			return cached;
		}

		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var config = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		var snapshot = new OutboundProxySnapshot(
			config.ProxyEnabled,
			config.ProxyType,
			config.ProxyHost,
			config.ProxyPort,
			config.ProxyUsername,
			config.ProxyPassword,
			config.ProxyBypassFilter,
			config.ProxyBypassLocalAddresses,
			config.CertificateValidation);
		cache.Set(CacheKey, snapshot, TimeSpan.FromSeconds(30));
		return snapshot;
	}

	/// <inheritdoc />
	public void Invalidate() => cache.Remove(CacheKey);
}
