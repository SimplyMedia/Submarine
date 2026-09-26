using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Events;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Api.Tests.Commands;

/// <summary>
///     Shared fixture wiring a real Sqlite in-memory database into the command queue.
/// </summary>
public sealed class CommandQueueFixture : IAsyncLifetime
{
	public ServiceProvider Provider { get; private set; } = null!;
	public SqliteConnection Connection { get; private set; } = null!;
	public FakeTimeProvider Clock { get; } = new();
	public IEventBus EventBus { get; } = Substitute.For<IEventBus>();

	public async ValueTask InitializeAsync()
	{
		Connection = new SqliteConnection("DataSource=:memory:");
		Connection.Open();

		var services = new ServiceCollection();
		services.TryAddSingleton<TimeProvider>(Clock);
		services.TryAddSingleton(EventBus);
		services.AddDbContext<SqliteSubmarineDbContext>(options =>
			options.UseSqlite(Connection).AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.TryAddSingleton<CommandRegistry>();
		services.TryAddSingleton<CommandExecutionRegistry>();
		services.TryAddSingleton<CommandQueue>();
		services.TryAddSingleton<ICommandQueue>(sp => sp.GetRequiredService<CommandQueue>());
		services.TryAddSingleton<ICommandCancellation>(sp => sp.GetRequiredService<CommandQueue>());

		Provider = services.BuildServiceProvider();
		var db = Provider.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await Provider.DisposeAsync();
		await Connection.DisposeAsync();
	}
}

public sealed class CommandQueueTests : IAsyncLifetime
{
	private readonly CommandQueueFixture _fixture = new();

	public ValueTask InitializeAsync() => _fixture.InitializeAsync();

	public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

	private CommandQueue Queue => _fixture.Provider.GetRequiredService<CommandQueue>();

	[Fact]
	public async Task EnqueueAsync_ShouldCreateQueuedRow_AndPublishEvent()
	{
		var row = await Queue.EnqueueAsync(new CommandCleanupCommand(), CommandTrigger.MANUAL, CommandPriority.HIGH, TestContext.Current.CancellationToken);

		row.Status.ShouldBe(CommandStatus.QUEUED);
		row.Name.ShouldBe("CommandCleanup");
		row.Priority.ShouldBe(CommandPriority.HIGH);
		row.BodyHash.ShouldNotBeEmpty();

		await _fixture.EventBus.Received(1).PublishAsync(
			Arg.Is<CommandUpdated>(e => e.CommandId == row.Id && e.Status == CommandStatus.QUEUED),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task EnqueueAsync_ShouldReturnExistingRow_WhenSameCommandIsAlreadyQueued()
	{
		var first = await Queue.EnqueueAsync(new CommandCleanupCommand(), CommandTrigger.MANUAL, cancellationToken: TestContext.Current.CancellationToken);
		var second = await Queue.EnqueueAsync(new CommandCleanupCommand(), CommandTrigger.MANUAL, cancellationToken: TestContext.Current.CancellationToken);

		second.Id.ShouldBe(first.Id);
		(await CountRows()).ShouldBe(1);
	}

	[Fact]
	public async Task EnqueueAsync_ShouldCreateSecondRow_WhenBodyDiffers()
	{
		var first = await Queue.EnqueueAsync(new TestEchoCommand("one"), CommandTrigger.MANUAL, cancellationToken: TestContext.Current.CancellationToken);
		var second = await Queue.EnqueueAsync(new TestEchoCommand("two"), CommandTrigger.MANUAL, cancellationToken: TestContext.Current.CancellationToken);

		second.Id.ShouldNotBe(first.Id);
		(await CountRows()).ShouldBe(2);
	}

	[Fact]
	public async Task TryCancelAsync_ShouldMarkQueuedRowCancelled()
	{
		var row = await Queue.EnqueueAsync(new CommandCleanupCommand(), CommandTrigger.MANUAL, cancellationToken: TestContext.Current.CancellationToken);

		var cancelled = await Queue.TryCancelAsync(row.Id, TestContext.Current.CancellationToken);

		cancelled.ShouldBeTrue();
		using var scope = _fixture.Provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var stored = await db.Commands.FindAsync([row.Id], TestContext.Current.CancellationToken);
		stored!.Status.ShouldBe(CommandStatus.CANCELLED);
		stored.EndedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task TryCancelAsync_ShouldReturnFalse_WhenRowIsUnknown()
		=> (await Queue.TryCancelAsync(424242, TestContext.Current.CancellationToken)).ShouldBeFalse();

	private async Task<int> CountRows()
	{
		using var scope = _fixture.Provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		return await db.Commands.CountAsync(TestContext.Current.CancellationToken);
	}
}

/// <summary>
///     Regression coverage for C-F29: a filtered unique index on (Name, BodyHash) backstops the
///     read-then-insert dedupe check, and EnqueueAsync recovers from the resulting race instead
///     of throwing.
/// </summary>
public sealed class CommandQueueConcurrencyTests : IAsyncDisposable
{
	private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"submarine-command-queue-{Guid.NewGuid()}.db");
	private readonly ServiceProvider _provider;

	public CommandQueueConcurrencyTests()
	{
		var services = new ServiceCollection();
		services.TryAddSingleton<TimeProvider>(new FakeTimeProvider());
		services.TryAddSingleton(Substitute.For<IEventBus>());
		services.AddDbContext<SqliteSubmarineDbContext>(options =>
			options.UseSqlite($"Data Source={_dbPath}").AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.TryAddSingleton<CommandExecutionRegistry>();
		services.TryAddSingleton<CommandQueue>();

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
	public async Task EnqueueAsync_ShouldNotCreateDuplicateRows_WhenTwoRequestsRaceThePrecheck()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var queue = _provider.GetRequiredService<CommandQueue>();

		// Both calls run their own "any queued/running row?" precheck concurrently, see nothing, and
		// both try to insert. Without the filtered unique index this creates two QUEUED rows; with it,
		// the DB rejects the second insert and EnqueueAsync falls back to returning the first row.
		var results = await Task.WhenAll(
			queue.EnqueueAsync(new TestEchoCommand("race"), CommandTrigger.MANUAL, cancellationToken: cancellationToken),
			queue.EnqueueAsync(new TestEchoCommand("race"), CommandTrigger.MANUAL, cancellationToken: cancellationToken));

		results[0].Id.ShouldBe(results[1].Id);

		await using var scope = _provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var rows = await db.Commands.Where(x => x.Name == "TestEcho").ToListAsync(cancellationToken);
		rows.Count.ShouldBe(1);
	}
}

/// <summary>Test command with a payload.</summary>
/// <param name="Message">Payload.</param>
public sealed record TestEchoCommand(string Message) : CommandBase;
