using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Submarine.Mappings.Infrastructure;

/// <summary>
/// Guards write endpoints. Writes need an X-Api-Key header matching Auth:AdminApiKey.
/// When no admin key is configured every write is rejected with 503.
/// Read endpoints stay public.
/// </summary>
public sealed class AdminApiKeyFilter(IConfiguration configuration) : IEndpointFilter
{
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		var expected = configuration["Auth:AdminApiKey"];
		if (string.IsNullOrEmpty(expected))
		{
			return Results.Problem(
				statusCode: StatusCodes.Status503ServiceUnavailable,
				title: "Writes are disabled",
				detail: "Auth:AdminApiKey is not configured on this instance.");
		}

		if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided) || !Matches(provided.ToString(), expected))
		{
			return Results.Problem(
				statusCode: StatusCodes.Status401Unauthorized,
				title: "Invalid API key",
				detail: "Provide the X-Api-Key header with the configured admin API key.");
		}

		return await next(context);
	}

	private static bool Matches(string provided, string expected)
	{
		var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
		var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
		return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
	}
}
