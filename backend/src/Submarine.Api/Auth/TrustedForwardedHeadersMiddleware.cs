using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Auth;

/// <summary>
///     Resolves the connection's remote address from X-Forwarded-For, but only through hops
///     whose own address is in the configured TrustedProxies list; this runs before routing so
///     every downstream consumer sees the corrected value.
/// </summary>
public static class TrustedForwardedHeadersMiddleware
{
	/// <summary>HttpContext item holding the prefetched auth configuration.</summary>
	public static readonly object AuthSnapshotItemKey = new();

	/// <summary>
	///     The middleware delegate, registered with <c>app.Use</c>.
	/// </summary>
	public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		var snapshot = await context.RequestServices.GetRequiredService<IAuthConfigProvider>()
			.GetSnapshotAsync(context.RequestAborted);
		context.Items[AuthSnapshotItemKey] = snapshot;
		if (context.Connection.RemoteIpAddress is { } peer)
		{
			var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
			if (!string.IsNullOrEmpty(forwardedFor))
			{
				if (snapshot.TrustedNetworks.Count > 0)
				{
					var chain = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
					context.Connection.RemoteIpAddress = ForwardedForResolver.Resolve(peer, chain, snapshot.TrustedNetworks);
				}
				else if (snapshot.AuthenticationRequired == AuthenticationRequiredType.DISABLED_FOR_LOCAL_ADDRESSES
					&& peer.IsLocalAddress())
				{
					await context.RequestServices.GetRequiredService<TrustedProxyWarningReporter>()
						.ReportAsync(context.RequestServices.GetRequiredService<SubmarineDbContext>(), context.RequestAborted);
					context.RequestServices.GetRequiredService<ILoggerFactory>()
						.CreateLogger("Submarine.Authentication")
						.LogWarning("A local reverse proxy forwarded a client address while TrustedProxies is empty; configure trusted proxies to protect local-address authentication");
				}
			}
		}

		await next(context);
	}
}

/// <summary>Persists the local-auth/trusted-proxy warning for the health API.</summary>
public sealed class TrustedProxyWarningReporter(IMemoryCache cache)
{
	private const string Source = "Authentication";
	private const string Message = "Trusted proxies is empty while local-address authentication is enabled; a local reverse proxy may expose the application without authentication";
	private const string CacheKey = "submarine.auth.trusted-proxy-warning";
	private readonly SemaphoreSlim gate = new(1, 1);

	/// <summary>Ensure the warning is visible through the health API.</summary>
	public async Task ReportAsync(SubmarineDbContext db, CancellationToken cancellationToken)
	{
		if (cache.TryGetValue(CacheKey, out _))
		{
			return;
		}

		await gate.WaitAsync(cancellationToken);
		try
		{
			if (cache.TryGetValue(CacheKey, out _))
			{
				return;
			}

			if (!await db.HealthIssues.AnyAsync(x => x.Source == Source && x.Message == Message, cancellationToken))
			{
				db.HealthIssues.Add(new HealthIssue
				{
					Type = HealthIssueType.WARNING,
					Source = Source,
					Message = Message
				});
				await db.SaveChangesAsync(cancellationToken);
			}

			cache.Set(CacheKey, true, TimeSpan.FromSeconds(30));
		}
		finally
		{
			gate.Release();
		}
	}
}
