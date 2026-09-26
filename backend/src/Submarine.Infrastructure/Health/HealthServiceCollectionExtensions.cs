using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Submarine.Core.Commands;
using Submarine.Infrastructure.Commands;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Updates;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Dependency injection registration for the health check stack.
/// </summary>
public static class HealthServiceCollectionExtensions
{
	/// <summary>
	///     Registers the health checks and the startup check trigger.
	/// </summary>
	public static IServiceCollection AddSubmarineHealth(this IServiceCollection services)
	{
		services.AddHttpClient("health", client => client.Timeout = TimeSpan.FromSeconds(15));
		services.AddScoped<IHealthCheck, IndexerHealthCheck>();
		services.AddScoped<IHealthCheck, DownloadClientHealthCheck>();
		services.AddScoped<IHealthCheck, RootFolderHealthCheck>();
		services.AddScoped<IHealthCheck, ServiceHealthCheck>();
		services.AddScoped<IHealthCheck, SettingsHealthCheck>();
		services.AddScoped<IHealthCheck, UpdateHealthCheck>();
		services.AddScoped<IHealthCheck, NotificationStatusCheck>();
		services.AddHostedService<StartupHealthCheckHostedService>();

		return services;
	}
}

/// <summary>
///     Enqueues the first health check shortly after startup.
/// </summary>
public sealed class StartupHealthCheckHostedService(
	ICommandQueue queue,
	ILogger<StartupHealthCheckHostedService> logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
			await queue.EnqueueAsync(new HealthCheckCommand(), CommandTrigger.SYSTEM, cancellationToken: stoppingToken);
		}
		catch (OperationCanceledException)
		{
			// shutting down before the check was enqueued
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to enqueue the startup health check");
		}
	}
}
