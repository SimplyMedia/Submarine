using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Auth;

/// <summary>
///     Claim types used by the cookie authentication session.
/// </summary>
public static class AuthClaimTypes
{
	/// <summary>Claim carrying the UTC ticks a cookie session was issued at.</summary>
	public const string AuthTime = "auth_time";
}

/// <summary>
///     Validates that a cookie session still belongs to an existing user and was issued no
///     earlier than the user's last change (a password change bumps <c>UpdatedAt</c>).
/// </summary>
public interface ICookiePrincipalValidator
{
	/// <summary>
	///     Whether the session behind this principal is still valid.
	/// </summary>
	Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default validator, caching the user's <c>UpdatedAt</c> for a few minutes so this does not
///     cost a database round trip on every request.
/// </summary>
public sealed class CookiePrincipalValidator(IMemoryCache cache, IServiceScopeFactory scopeFactory) : ICookiePrincipalValidator
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
	private const string CacheKeyPrefix = "submarine.auth.principal.";

	/// <inheritdoc />
	public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
	{
		var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
		var authTimeClaim = principal.FindFirstValue(AuthClaimTypes.AuthTime);
		if (idClaim is null || !int.TryParse(idClaim, out var userId)
			|| authTimeClaim is null || !long.TryParse(authTimeClaim, out var authTimeTicks))
		{
			return false;
		}

		var cacheKey = CacheKeyPrefix + userId;
		if (!cache.TryGetValue(cacheKey, out DateTime? updatedAt))
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			updatedAt = await db.Users.AsNoTracking()
				.Where(x => x.Id == userId)
				.Select(x => (DateTime?)x.UpdatedAt)
				.FirstOrDefaultAsync(cancellationToken);
			cache.Set(cacheKey, updatedAt, CacheDuration);
		}

		if (updatedAt is null)
		{
			return false;
		}

		var authTime = new DateTime(authTimeTicks, DateTimeKind.Utc);
		return authTime >= updatedAt.Value;
	}
}
