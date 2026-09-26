using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Ticks every 60 seconds and enqueues due scheduled tasks as scheduled commands.
/// </summary>
public sealed class SchedulerHostedService(
	IServiceScopeFactory scopeFactory,
	ICommandQueue queue,
	CommandRegistry registry,
	TimeProvider timeProvider,
	ILogger<SchedulerHostedService> logger) : BackgroundService
{
	private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (SubmarineDesignTime.IsDocumentGeneration())
		{
			return;
		}

		using var timer = new PeriodicTimer(TickInterval);
		try
		{
			do
			{
				await TickAsync(stoppingToken);
			}
			while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Shutting down.
		}
	}

	private async Task TickAsync(CancellationToken cancellationToken)
	{
		var now = timeProvider.GetUtcNow().UtcDateTime;

		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			var due = await db.ScheduledTasks
				.Where(x => x.IntervalMinutes > 0 && (x.NextRun == null || x.NextRun <= now))
				.ToListAsync(cancellationToken);

			foreach (var task in due)
			{
				if (registry.TryResolve(task.Name, out var commandType))
				{
					await EnqueueAsync(task, commandType, cancellationToken);
				}
				else
				{
					logger.LogDebug(
						"Scheduled task {Name} has no command registered yet, skipping this tick",
						task.Name);
				}

				task.LastRun = now;
				task.NextRun = ScheduleCalculator.ComputeNextRun(now, task.IntervalMinutes, now);
			}

			await db.SaveChangesAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Scheduler tick failed");
		}
	}

	private async Task EnqueueAsync(ScheduledTask task, Type commandType, CancellationToken cancellationToken)
	{
		try
		{
			var command = (ICommand)Activator.CreateInstance(commandType)!;
			await queue.EnqueueAsync(command, CommandTrigger.SCHEDULED, CommandPriority.NORMAL, cancellationToken);
		}
		catch (MissingMethodException ex)
		{
			logger.LogError(ex, "Scheduled command {Name} has no parameterless constructor", task.Name);
		}
	}
}
