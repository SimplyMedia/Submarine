using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Submarine.Core.Common;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Executes queued commands with limited parallelism, high priority first and at most
///     one concurrent execution per command name. Running rows left behind by a crash are
///     marked failed on startup.
/// </summary>
public sealed class CommandExecutorHostedService(
	IServiceScopeFactory scopeFactory,
	CommandQueue queue,
	IEventBus eventBus,
	CommandRegistry registry,
	CommandExecutionRegistry executionRegistry,
	TimeProvider timeProvider,
	ILogger<CommandExecutorHostedService> logger) : BackgroundService
{
	private const int MaxParallelism = 2;

	private readonly HashSet<string> _runningNames = [];
	private readonly SemaphoreSlim _stateLock = new(1, 1);
	private int _runningCount;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (SubmarineDesignTime.IsDocumentGeneration())
		{
			return;
		}

		await MarkOrphanedRunningAsFailedAsync(stoppingToken);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await DrainAsync(stoppingToken);
				await queue.SignalReader.WaitToReadAsync(stoppingToken);
				while (queue.SignalReader.TryRead(out _))
				{
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Command executor loop failed");
			}
		}
	}

	private async Task DrainAsync(CancellationToken stoppingToken)
	{
		while (Volatile.Read(ref _runningCount) < MaxParallelism && !stoppingToken.IsCancellationRequested)
		{
			var claimed = await TryClaimNextAsync(stoppingToken);
			if (claimed is null)
			{
				break;
			}

			Interlocked.Increment(ref _runningCount);
			// Fire-and-forget: a freed slot must be reclaimable as soon as any single execution finishes,
			// not only once every execution started in this drain pass has completed. ExecuteAndReleaseAsync
			// calls queue.Wake() when it finishes, which re-enters DrainAsync via the executor's main loop.
			_ = ExecuteAndReleaseAsync(claimed, stoppingToken);
		}
	}

	private async Task<Command?> TryClaimNextAsync(CancellationToken stoppingToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();

		await _stateLock.WaitAsync(stoppingToken);
		try
		{
			var candidates = await db.Commands
				.Where(x => x.Status == CommandStatus.QUEUED)
				.OrderByDescending(x => x.Priority)
				.ThenBy(x => x.Id)
				.Take(50)
				.ToListAsync(stoppingToken);
			var candidate = CommandSelector.SelectNext(candidates, _runningNames);
			if (candidate is null)
			{
				return null;
			}

			_runningNames.Add(candidate.Name);
			candidate.Status = CommandStatus.RUNNING;
			candidate.StartedAt = timeProvider.GetUtcNow().UtcDateTime;
			await db.SaveChangesAsync(stoppingToken);
			await PublishAsync(candidate, stoppingToken);
			return candidate;
		}
		finally
		{
			_stateLock.Release();
		}
	}

	private async Task ExecuteAndReleaseAsync(Command claimed, CancellationToken stoppingToken)
	{
		try
		{
			await ExecuteAsync(claimed, stoppingToken);
		}
		finally
		{
			await _stateLock.WaitAsync();
			try
			{
				_runningNames.Remove(claimed.Name);
			}
			finally
			{
				_stateLock.Release();
			}

			Interlocked.Decrement(ref _runningCount);
			queue.Wake();
		}
	}

	private async Task ExecuteAsync(Command claimed, CancellationToken stoppingToken)
	{
		if (!registry.TryResolve(claimed.Name, out var commandType))
		{
			await FinishAsync(claimed.Id, CommandStatus.FAILED, $"No command type registered for '{claimed.Name}'", null, CancellationToken.None);
			return;
		}

		using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		executionRegistry.Register(claimed.Id, cancellationTokenSource);

		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
			var handler = scope.ServiceProvider.GetRequiredService(handlerType);
			var command = (ICommand)JsonSerializer.Deserialize(claimed.Body, commandType, SubmarineJson.Default)!;
			var context = new CommandContext(
				claimed.Id,
				scope.ServiceProvider.GetRequiredService<SubmarineDbContext>(),
				eventBus,
				timeProvider);

			var executeMethod = handlerType.GetMethod("ExecuteAsync")
				?? throw new InvalidOperationException($"Handler for '{claimed.Name}' has no ExecuteAsync method");
			await (Task)executeMethod.Invoke(handler, [command, context, cancellationTokenSource.Token])!;

			await FinishAsync(claimed.Id, CommandStatus.COMPLETED, null, null, cancellationTokenSource.Token);
		}
		catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
		{
			await FinishAsync(claimed.Id, CommandStatus.CANCELLED, "Cancelled", null, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Command {Name} ({Id}) failed", claimed.Name, claimed.Id);
			await FinishAsync(claimed.Id, CommandStatus.FAILED, ex.Message, ex.ToString(), CancellationToken.None);
		}
		finally
		{
			executionRegistry.Unregister(claimed.Id);
		}
	}

	private async Task FinishAsync(
		int commandId,
		CommandStatus status,
		string? message,
		string? exception,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			var row = await db.Commands.FindAsync([commandId], cancellationToken);
			if (row is null)
			{
				return;
			}

			row.Status = status;
			row.Message = message;
			row.Exception = exception;
			row.EndedAt = timeProvider.GetUtcNow().UtcDateTime;
			if (status == CommandStatus.COMPLETED)
			{
				row.Progress = 100;
			}

			await db.SaveChangesAsync(cancellationToken);
			await PublishAsync(row, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to persist final state of command {Id}", commandId);
		}
	}

	private async Task MarkOrphanedRunningAsFailedAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
			var orphaned = await db.Commands
				.Where(x => x.Status == CommandStatus.RUNNING)
				.ToListAsync(cancellationToken);
			foreach (var row in orphaned)
			{
				row.Status = CommandStatus.FAILED;
				row.Message = "Interrupted by restart";
				row.EndedAt = timeProvider.GetUtcNow().UtcDateTime;
			}

			await db.SaveChangesAsync(cancellationToken);
			foreach (var row in orphaned)
			{
				await PublishAsync(row, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to mark orphaned running commands as failed");
		}
	}

	private async Task PublishAsync(Command row, CancellationToken cancellationToken)
		=> await eventBus.PublishAsync(
			new CommandUpdated(row.Id, row.Name, row.Status, row.Progress, row.Message, row.StartedAt, row.EndedAt),
			cancellationToken);
}
