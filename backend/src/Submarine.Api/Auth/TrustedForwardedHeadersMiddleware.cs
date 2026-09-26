using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;

namespace Submarine.Api.Auth;

/// <summary>
///     Resolves the connection's remote address from X-Forwarded-For, but only through hops
///     whose own address is in the configured TrustedProxies list; this runs before routing so
///     every downstream consumer of <see cref="HttpContext.Connection" />'s RemoteIpAddress
///     (the login rate limiter, the local-address auth bypass) sees the corrected value and
///     cannot be fooled by a spoofed header from an untrusted peer.
/// </summary>
public static class TrustedForwardedHeadersMiddleware
{
	/// <summary>
	///     The middleware delegate, registered with <c>app.Use</c>.
	/// </summary>
	public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		var snapshot = await context.RequestServices.GetRequiredService<IAuthConfigProvider>()
			.GetSnapshotAsync(context.RequestAborted);
		if (snapshot.TrustedNetworks.Count > 0 && context.Connection.RemoteIpAddress is { } peer)
		{
			var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
			if (!string.IsNullOrEmpty(forwardedFor))
			{
				var chain = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				context.Connection.RemoteIpAddress = ForwardedForResolver.Resolve(peer, chain, snapshot.TrustedNetworks);
			}
		}

		await next(context);
	}
}
