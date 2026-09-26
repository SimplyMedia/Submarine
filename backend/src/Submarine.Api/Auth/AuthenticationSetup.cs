using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Submarine.Core.Enums;
using Submarine.Core.Net;
using Submarine.Infrastructure.Auth;

namespace Submarine.Api.Auth;

/// <summary>
///     Scheme names and configuration of the authentication stack.
/// </summary>
public static class AuthenticationSetup
{
	/// <summary>Top level policy scheme choosing between API key, anonymous and cookie.</summary>
	public const string Scheme = "Submarine.Auth";

	/// <summary>Cookie scheme for UI users.</summary>
	public const string CookieScheme = "Submarine.Cookie";

	/// <summary>API key scheme for external tools and SignalR.</summary>
	public const string ApiKeyScheme = "ApiKey";

	/// <summary>HTTP Basic scheme, used when AuthMethod is Basic.</summary>
	public const string BasicScheme = "Submarine.Basic";

	/// <summary>Scheme authenticating every request when AuthMethod is None or External.</summary>
	public const string AnonymousScheme = "Submarine.Anonymous";

	/// <summary>Display name.</summary>
	public const string DisplayName = "Submarine";

	/// <summary>
	///     Everything not marked anonymous requires authentication.
	/// </summary>
	public static AuthorizationPolicy FallbackPolicy { get; } =
		new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build();

	/// <summary>
	///     Reject a cookie session when its user was deleted or the session was issued before
	///     the user's last change (password changes bump <c>UpdatedAt</c>).
	/// </summary>
	public static async Task ValidateCookiePrincipalAsync(CookieValidatePrincipalContext context)
	{
		var validator = context.HttpContext.RequestServices.GetRequiredService<ICookiePrincipalValidator>();
		if (context.Principal is null
			|| !await validator.IsValidAsync(context.Principal, context.HttpContext.RequestAborted))
		{
			context.RejectPrincipal();
			await context.HttpContext.SignOutAsync(CookieScheme);
		}
	}

	/// <summary>
	///     Choose the concrete scheme: API key when provided, otherwise anonymous
	///     when auth is disabled, otherwise the login cookie.
	/// </summary>
	public static void ConfigurePolicyScheme(PolicySchemeOptions options)
	{
		options.ForwardDefaultSelector = context =>
		{
			if (context.Request.Headers.ContainsKey("X-Api-Key")
				|| context.Request.Query.ContainsKey("apikey"))
			{
				return ApiKeyScheme;
			}

			// Cached for 30 seconds, so the blocking wait completes synchronously.
			var snapshot = context.RequestServices
				.GetRequiredService<IAuthConfigProvider>()
				.GetSnapshotAsync()
				.GetAwaiter()
				.GetResult();

			// The forwarded-headers middleware (Program.cs, runs before routing) has already
			// resolved the connection's remote address against the trusted proxy list, so this
			// check cannot be fooled by a spoofed X-Forwarded-For from an untrusted peer.
			if (snapshot.AuthenticationRequired == AuthenticationRequiredType.DISABLED_FOR_LOCAL_ADDRESSES
				&& context.Connection.RemoteIpAddress is { } remoteIp
				&& remoteIp.IsLocalAddress())
			{
				return AnonymousScheme;
			}

			return snapshot.Method switch
			{
				AuthMethod.NONE or AuthMethod.EXTERNAL => AnonymousScheme,
				AuthMethod.BASIC => BasicScheme,
				_ => CookieScheme
			};
		};
	}

	/// <summary>
	///     Cookie options: strict, httpOnly, sliding 14 days, JSON 401 instead of redirects.
	/// </summary>
	public static void ConfigureCookie(CookieAuthenticationOptions options)
	{
		options.Cookie.Name = "Submarine.Auth";
		options.Cookie.HttpOnly = true;
		options.Cookie.SameSite = SameSiteMode.Strict;
		options.SlidingExpiration = true;
		options.ExpireTimeSpan = TimeSpan.FromDays(14);
		options.Events.OnRedirectToLogin = context =>
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		};
		options.Events.OnRedirectToAccessDenied = context =>
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return Task.CompletedTask;
		};
		options.Events.OnValidatePrincipal = ValidateCookiePrincipalAsync;
	}
}

/// <summary>
///     Authenticates every request as an anonymous administrator. Active when AuthMethod is None.
/// </summary>
public sealed class AnonymousAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	/// <summary>Name of the anonymous principal.</summary>
	public const string AnonymousUser = "anonymous-admin";

	/// <inheritdoc />
	public AnonymousAuthenticationHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		System.Text.Encodings.Web.UrlEncoder encoder)
		: base(options, logger, encoder)
	{
	}

	/// <inheritdoc />
	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, AnonymousUser)], Scheme.Name);
		return Task.FromResult(AuthenticateResult.Success(
			new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
	}

	/// <inheritdoc />
	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.StatusCode = StatusCodes.Status401Unauthorized;
		return Task.CompletedTask;
	}
}

/// <summary>
///     Authenticates requests carrying X-Api-Key header or apikey query parameter
///     against the configured global key, compared in constant time.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	System.Text.Encodings.Web.UrlEncoder encoder,
	IAuthConfigProvider authConfigProvider)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	/// <inheritdoc />
	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var provided = Request.Headers["X-Api-Key"].FirstOrDefault()
			?? Request.Query["apikey"].FirstOrDefault();
		if (string.IsNullOrEmpty(provided))
		{
			return AuthenticateResult.NoResult();
		}

		var snapshot = await authConfigProvider.GetSnapshotAsync(Context.RequestAborted);
		if (string.IsNullOrEmpty(snapshot.ApiKey))
		{
			return AuthenticateResult.Fail("API key is not configured");
		}

		if (!FixedTimeEquals(provided, snapshot.ApiKey))
		{
			return AuthenticateResult.Fail("Invalid API key");
		}

		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.Name, "api-key"), new Claim(ClaimTypes.Role, "apikey")],
			Scheme.Name);
		return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
	}

	/// <inheritdoc />
	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.StatusCode = StatusCodes.Status401Unauthorized;
		return Task.CompletedTask;
	}

	private static bool FixedTimeEquals(string left, string right)
	{
		var leftBytes = Encoding.UTF8.GetBytes(left);
		var rightBytes = Encoding.UTF8.GetBytes(right);
		return leftBytes.Length == rightBytes.Length
			&& System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
	}
}

/// <summary>
///     Authenticates requests carrying an HTTP Basic Authorization header against the Users
///     table. Active when AuthMethod is Basic.
/// </summary>
public sealed class BasicAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	System.Text.Encodings.Web.UrlEncoder encoder,
	IUserCredentialVerifier verifier)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	private const string Prefix = "Basic ";

	/// <inheritdoc />
	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var header = Request.Headers.Authorization.FirstOrDefault();
		if (header is null || !header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
		{
			return AuthenticateResult.Fail("Authorization header missing or not Basic");
		}

		string decoded;
		try
		{
			decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[Prefix.Length..]));
		}
		catch (FormatException)
		{
			return AuthenticateResult.Fail("Malformed Basic authorization header");
		}

		var parts = decoded.Split(':', 2);
		if (parts.Length != 2)
		{
			return AuthenticateResult.Fail("Malformed Basic authorization header");
		}

		var user = await verifier.VerifyAsync(parts[0], parts[1], Context.RequestAborted);
		if (user is null)
		{
			return AuthenticateResult.Fail("The username or password is not correct");
		}

		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username)],
			Scheme.Name);
		return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
	}

	/// <inheritdoc />
	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.Headers.WWWAuthenticate = $"Basic realm=\"{AuthenticationSetup.DisplayName}\"";
		Response.StatusCode = StatusCodes.Status401Unauthorized;
		return Task.CompletedTask;
	}
}
