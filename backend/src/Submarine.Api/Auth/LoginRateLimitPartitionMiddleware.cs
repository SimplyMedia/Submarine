using System.Text.Json;
using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;

namespace Submarine.Api.Auth;

/// <summary>Derives a per-username login limiter key when a forwarded client cannot be trusted.</summary>
public static class LoginRateLimitPartitionMiddleware
{
	/// <summary>Request item holding the selected rate-limit partition.</summary>
	public static readonly object PartitionKeyItemKey = new();

	/// <summary>Read the login username before rate limiting, preserving the body for endpoint binding.</summary>
	public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		if (context.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
			&& context.Items.TryGetValue(TrustedForwardedHeadersMiddleware.AuthSnapshotItemKey, out var value)
			&& value is AuthSnapshot { TrustedNetworks.Count: 0 }
			&& !string.IsNullOrWhiteSpace(context.Request.Headers["X-Forwarded-For"])
			&& context.Connection.RemoteIpAddress is { } peer
			&& peer.IsLocalAddress())
		{
			context.Request.EnableBuffering();
			try
			{
				using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
				var root = document.RootElement;
				if (root.ValueKind == JsonValueKind.Object
					&& root.TryGetProperty("username", out var username)
					&& username.ValueKind == JsonValueKind.String
					&& !string.IsNullOrWhiteSpace(username.GetString()))
				{
					context.Items[PartitionKeyItemKey] = $"user:{username.GetString()!.Trim().ToUpperInvariant()}";
				}
			}
			catch (JsonException)
			{
				// The endpoint remains responsible for reporting malformed request bodies.
			}
			finally
			{
				context.Request.Body.Position = 0;
			}
		}

		await next(context);
	}
}
