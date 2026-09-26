using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.Tests.Commands;

/// <summary>
///     Regression coverage for the executor's fire-and-forget scheduling (C-F19) and priority-first
///     claim ordering (C-F29).
/// </summary>
public sealed class CommandExecutorHostedServiceTests : IAsyncDisposable
{
	private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"submarine-executor-{Guid.NewGuid()}.db");
	private readonly ServiceProvider _provider;

	public CommandExecutorHostedServiceTests()
	{
		var services = new ServiceCollection();
		services.TryAddSingleton<TimeProvider>(new FakeTimeProvider());
		services.TryAddSingleton(Substitute.For<IEventBus>());
		// A real file (not a single shared in-memory connection object) so the executor's concurrent
		// scopes get independent connections, matching how it behaves against a real database.
		services.AddDbContext<SqliteSubmarineDbContext>(options =>
			options.UseSqlite($"Data Source={_dbPath}").AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.TryAddSingleton<CommandRegistry>();
		services.TryAddSingleton<CommandExecutionRegistry>();
		services.TryAddSingleton<CommandQueue>();
		services.AddScoped<ICommandHandler<TestEchoCommand>, GatedEchoHandler>();
		services.AddScoped<ICommandHandler<TestGateCommand>, GatedCommandHandler>();
		services.AddSingleton<Microsoft.Extensions.Logging.ILogger<CommandExecutorHostedService>>(
			NullLogger<CommandExecutorHostedService>.Instance);
		services.AddSingleton<CommandExecutorHostedService>();

		_provider = services.BuildServiceProvider();
		_provider.GetRequiredService<SubmarineDbContext>().Database.EnsureCreated();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		File.Delete(_dbPath);
		File.Delete(_dbPath + "-wal");
		File.Delete(_dbPath + "-shm");
	}

	[Fact]
	public async Task Executor_ShouldReclaimFreedSlot_WithoutWaitingForOtherRunningExecutions()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var gateKey = $"slow-{Guid.NewGuid()}";
		GatedEchoHandler.Gates[gateKey] = gate;

		var queue = _provider.GetRequiredService<CommandQueue>();
		// "slow" is a different command type than "fast"/"third" on purpose: the executor never runs
		// two commands of the same name concurrently, which would confound this test with the
		// (correct, separate) single-flight-per-name behaviour instead of exercising slot reclaim.
		var slow = await queue.EnqueueAsync(new TestGateCommand(gateKey), CommandTrigger.MANUAL, cancellationToken: cancellationToken);
		await queue.EnqueueAsync(new TestEchoCommand("fast"), CommandTrigger.MANUAL, cancellationToken: cancellationToken);
		var third = await queue.EnqueueAsync(new TestEchoCommand("third"), CommandTrigger.MANUAL, cancellationToken: cancellationToken);

		var executor = _provider.GetRequiredService<CommandExecutorHostedService>();
		await executor.StartAsync(cancellationToken);
		try
		{
			// With MaxParallelism = 2, "slow" and "fast" fill both slots. "third" can only start once a slot
			// is freed. If the executor still awaited WhenAll on the whole batch before reclaiming slots
			// (the pre-fix behaviour), "third" would stay QUEUED until "slow" also finished, which never
			// happens here because its gate is never released during this assertion.
			var thirdCompleted = await WaitForStatusAsync(third.Id, CommandStatus.COMPLETED, TimeSpan.FromSeconds(5), cancellationToken);
			thirdCompleted.ShouldBeTrue("the freed 'fast' slot should have been reclaimed for 'third' without waiting for 'slow'");

			(await GetStatusAsync(slow.Id, cancellationToken)).ShouldBe(CommandStatus.RUNNING);
		}
		finally
		{
			gate.TrySetResult();
			await WaitForStatusAsync(slow.Id, CommandStatus.COMPLETED, TimeSpan.FromSeconds(5), cancellationToken);
			await executor.StopAsync(cancellationToken);
			GatedEchoHandler.Gates.TryRemove(gateKey, out _);
		}
	}

	[Fact]
	public async Task Executor_ShouldClaimHighPriorityRow_EvenPastTheFirstFiftyQueuedRows()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var queue = _provider.GetRequiredService<CommandQueue>();

		// 55 low-priority rows queued ahead of a high-priority row by insertion order alone. Under the
		// old "order by Id, take 50" claim query this row would never appear in the claimable batch;
		// the fixed "order by Priority desc, Id" query must surface it first regardless of position.
		for (var i = 0; i < 55; i++)
		{
			await queue.EnqueueAsync(new TestEchoCommand($"filler-{i}"), CommandTrigger.MANUAL, CommandPriority.LOW, cancellationToken);
		}

		var high = await queue.EnqueueAsync(new TestEchoCommand("urgent"), CommandTrigger.MANUAL, CommandPriority.HIGH, cancellationToken);

		var executor = _provider.GetRequiredService<CommandExecutorHostedService>();
		await executor.StartAsync(cancellationToken);
		try
		{
			var highCompleted = await WaitForStatusAsync(high.Id, CommandStatus.COMPLETED, TimeSpan.FromSeconds(5), cancellationToken);
			highCompleted.ShouldBeTrue("a HIGH priority row must be claimable even when 55 lower priority rows precede it by id");
		}
		finally
		{
			await executor.StopAsync(cancellationToken);
		}
	}

	private async Task<CommandStatus> GetStatusAsync(int commandId, CancellationToken cancellationToken)
	{
		await using var scope = _provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		return await db.Commands.AsNoTracking().Where(x => x.Id == commandId).Select(x => x.Status).SingleAsync(cancellationToken);
	}

	private async Task<bool> WaitForStatusAsync(int commandId, CommandStatus status, TimeSpan timeout, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (await GetStatusAsync(commandId, cancellationToken) == status)
			{
				return true;
			}

			await Task.Delay(20, cancellationToken);
		}

		return false;
	}
}

/// <summary>Test handler that blocks on a named gate when the command message matches a registered key.</summary>
file sealed class GatedEchoHandler : ICommandHandler<TestEchoCommand>
{
	public static ConcurrentDictionary<string, TaskCompletionSource> Gates { get; } = new();

	public Task ExecuteAsync(TestEchoCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> Gates.TryGetValue(command.Message, out var gate) ? gate.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
}

/// <summary>Test command of a distinct registry name from <see cref="TestEchoCommand" />, so a blocked
/// instance of this type does not single-flight-block unrelated TestEchoCommand rows.</summary>
/// <param name="Key">Gate key looked up in <see cref="GatedEchoHandler.Gates" />.</param>
public sealed record TestGateCommand(string Key) : CommandBase;

file sealed class GatedCommandHandler : ICommandHandler<TestGateCommand>
{
	public Task ExecuteAsync(TestGateCommand command, ICommandContext context, CancellationToken cancellationToken = default)
		=> GatedEchoHandler.Gates.TryGetValue(command.Key, out var gate) ? gate.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
}
