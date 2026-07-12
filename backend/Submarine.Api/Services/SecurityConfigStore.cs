using Submarine.Core.Config;

namespace Submarine.Api.Services;

/// <summary>
///     In-memory cache of the active <see cref="SecurityConfig" /> for the API key middleware, refreshed every 30
///     seconds or immediately on a security settings update
/// </summary>
public class SecurityConfigStore
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

	private readonly IServiceScopeFactory _scopeFactory;

	private SecurityConfig? _cached;

	private DateTimeOffset _cachedAt;

	public SecurityConfigStore(IServiceScopeFactory scopeFactory)
		=> _scopeFactory = scopeFactory;

	/// <summary>
	///     Returns the cached config, reloading it from the database when stale
	/// </summary>
	/// <returns>The active <see cref="SecurityConfig" /></returns>
	public async Task<SecurityConfig> GetAsync()
	{
		var cached = _cached;

		if (cached != null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
			return cached;

		using var scope = _scopeFactory.CreateScope();

		var config = await scope.ServiceProvider.GetRequiredService<SettingsService>().GetSecurityConfigAsync();

		Set(config);

		return config;
	}

	/// <summary>
	///     Replaces the cached config, e.g. after a security settings update
	/// </summary>
	/// <param name="config">config to cache</param>
	public void Set(SecurityConfig config)
	{
		_cached = config;
		_cachedAt = DateTimeOffset.UtcNow;
	}
}
