using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Checks that the API key is long enough to resist brute forcing.
/// </summary>
public sealed class ApiKeyValidationHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	private const int MinimumLength = 20;

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var general = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		return general.ApiKey.Length < MinimumLength
			? [new(HealthIssueType.WARNING, "Authentication",
				$"The API key should be at least {MinimumLength} characters long, generate a new one in settings",
				HealthWikiLinks.For("invalid-api-key"))]
			: [];
	}
}

/// <summary>Warns when local-address authentication is configured without trusted proxies.</summary>
public sealed class TrustedProxyAuthenticationHealthCheck(SubmarineDbContext db) : IHealthCheck
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		var config = await db.GeneralConfig.AsNoTracking().SingleAsync(cancellationToken);
		return config.AuthenticationRequired == AuthenticationRequiredType.DISABLED_FOR_LOCAL_ADDRESSES
			&& string.IsNullOrWhiteSpace(config.TrustedProxies)
			? [new(HealthIssueType.WARNING, "Authentication",
				"Trusted proxies is empty while local-address authentication is enabled; a local reverse proxy may expose the application without authentication",
				null)]
			: [];
	}
}

/// <summary>
///     Checks the local system clock against a remote HTTP server's clock, using the standard
///     Date response header so any reachable server works.
/// </summary>
public sealed class SystemTimeHealthCheck(
	IHttpClientFactory httpClientFactory,
	TimeProvider timeProvider,
	ILogger<SystemTimeHealthCheck> logger) : IHealthCheck
{
	private const string Source = "System time";
	private const string TimeCheckUrl = "https://github.com";

	/// <inheritdoc />
	public async Task<IReadOnlyList<HealthIssueSnapshot>> CheckAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var client = httpClientFactory.CreateClient("health");
			using var request = new HttpRequestMessage(HttpMethod.Head, TimeCheckUrl);
			using var response = await client.SendAsync(request, cancellationToken);
			if (response.Headers.Date is not { } remoteTime)
			{
				return [];
			}

			var localTime = timeProvider.GetUtcNow();
			if (Math.Abs((remoteTime - localTime).TotalDays) >= 1)
			{
				return [new(HealthIssueType.ERROR, Source,
					"The system clock is off by more than a day, fix it or releases and schedules will misbehave",
					HealthWikiLinks.For("system-time-off"))];
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Unable to verify the system time");
		}

		return [];
	}
}
