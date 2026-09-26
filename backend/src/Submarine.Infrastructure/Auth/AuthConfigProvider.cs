using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Auth;

/// <summary>
///     Immutable snapshot of the auth relevant configuration.
/// </summary>
public sealed record AuthSnapshot(
	AuthMethod Method,
	string ApiKey,
	string UrlBase,
	AuthenticationRequiredType AuthenticationRequired,
	IReadOnlyList<IPNetwork> TrustedNetworks);

/// <summary>
///     Provides the current auth configuration with a short cache so request hot paths
///     (including the synchronous policy scheme selector) do not hit the database.
/// </summary>
public interface IAuthConfigProvider
{
	/// <summary>
	///     Get the current snapshot, cached for a few seconds.
	/// </summary>
	Task<AuthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Drop the cache so changes take effect immediately.
	/// </summary>
	void Invalidate();
}

/// <summary>
///     Default provider reading the GeneralConfig singleton with a 30 second cache.
/// </summary>
public sealed class AuthConfigProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory, LoggingLevelSwitch logLevelSwitch) : IAuthConfigProvider
{
	private const string CacheKey = "submarine.auth.snapshot";

	/// <inheritdoc />
	public async Task<AuthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out AuthSnapshot? cached) && cached is not null)
		{
			return cached;
		}

		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var config = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		if (Enum.TryParse<LogEventLevel>(config.LogLevel, true, out var level))
		{
			logLevelSwitch.MinimumLevel = level;
		}

		var snapshot = new AuthSnapshot(
			config.AuthMethod,
			config.ApiKey,
			config.UrlBase,
			config.AuthenticationRequired,
			ForwardedForResolver.ParseNetworks(config.TrustedProxies));
		cache.Set(CacheKey, snapshot, TimeSpan.FromSeconds(30));
		return snapshot;
	}

	/// <inheritdoc />
	public void Invalidate() => cache.Remove(CacheKey);
}
