using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Submarine.Api.Services;
using Submarine.Core.Config;

namespace Submarine.Api.Middleware;

/// <summary>
///     Enforces API key authentication for requests to /api paths (other paths, e.g. /_status, pass through).
///     Precedence: the appsettings key "Auth:Method" (None|ApiKey), when present, overrides the stored
///     <see cref="SecurityConfig" /> method. When absent in the Development environment, a stored API_KEY method is
///     treated as NONE so local development and swagger keep working.
/// </summary>
public class ApiKeyMiddleware
{
	private const string HeaderName = "X-Api-Key";

	private const string QueryName = "apikey";

	private readonly IConfiguration _configuration;

	private readonly IHostEnvironment _environment;

	private readonly ILogger<ApiKeyMiddleware> _logger;

	private readonly RequestDelegate _next;

	private readonly SecurityConfigStore _store;

	private bool _developmentDefaultLogged;

	private bool _unrecognizedMethodLogged;

	public ApiKeyMiddleware(RequestDelegate next, SecurityConfigStore store, IConfiguration configuration,
		IHostEnvironment environment, ILogger<ApiKeyMiddleware> logger)
	{
		_next = next;
		_store = store;
		_configuration = configuration;
		_environment = environment;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		var config = await _store.GetAsync();

		if (GetEffectiveMethod(config) == AuthenticationMethod.NONE)
		{
			await _next(context);
			return;
		}

		var providedKey = context.Request.Headers[HeaderName].FirstOrDefault()
		                  ?? context.Request.Query[QueryName].FirstOrDefault();

		if (IsValidKey(providedKey, config.ApiKey))
		{
			await _next(context);
			return;
		}

		context.Response.StatusCode = StatusCodes.Status401Unauthorized;

		await context.Response.WriteAsJsonAsync(new ProblemDetails
		{
			Status = StatusCodes.Status401Unauthorized,
			Title = "Unauthorized",
			Detail = "A valid API key is required"
		}, options: null, contentType: "application/problem+json");
	}

	// constant-time compare avoids leaking the key through response timing; a null/empty stored key never authorizes
	private static bool IsValidKey(string? providedKey, string? storedKey)
	{
		if (providedKey == null || string.IsNullOrEmpty(storedKey))
			return false;

		return CryptographicOperations.FixedTimeEquals(
			Encoding.UTF8.GetBytes(providedKey), Encoding.UTF8.GetBytes(storedKey));
	}

	private AuthenticationMethod GetEffectiveMethod(SecurityConfig config)
	{
		var configured = _configuration.GetValue<string>("Auth:Method");

		if (configured != null)
		{
			// an unrecognized value must fail closed rather than silently disabling authentication
			var normalized = configured.Replace("_", "");

			if (normalized.Equals("ApiKey", StringComparison.OrdinalIgnoreCase))
				return AuthenticationMethod.API_KEY;

			if (normalized.Equals("None", StringComparison.OrdinalIgnoreCase))
				return AuthenticationMethod.NONE;

			if (!_unrecognizedMethodLogged)
			{
				_logger.LogWarning("Unrecognized Auth:Method '{Value}', enforcing API key", configured);
				_unrecognizedMethodLogged = true;
			}

			return AuthenticationMethod.API_KEY;
		}

		if (_environment.IsDevelopment() && config.Method == AuthenticationMethod.API_KEY)
		{
			if (!_developmentDefaultLogged)
			{
				_logger.LogDebug("Auth:Method is not configured, treating stored API_KEY method as NONE in Development");
				_developmentDefaultLogged = true;
			}

			return AuthenticationMethod.NONE;
		}

		return config.Method;
	}
}
