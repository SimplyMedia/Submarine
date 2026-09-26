using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.Commands;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Commands;

/// <summary>
///     Asserts command and log row pruning performed by CommandCleanup.
/// </summary>
public sealed class CommandCleanupCommandHandlerTests : IAsyncLifetime
{
	private SqliteConnection _dbConnection = null!;
	private SqliteConnection _logConnection = null!;
	private ServiceProvider _provider = null!;
	private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-15T00:00:00Z"));

	public async ValueTask InitializeAsync()
	{
		_dbConnection = new SqliteConnection("DataSource=:memory:");
		_dbConnection.Open();
		_logConnection = new SqliteConnection("DataSource=:memory:");
		_logConnection.Open();

		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(_clock);
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_dbConnection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.AddDbContext<SqliteLogDbContext>(options => options.UseSqlite(_logConnection));
		services.AddScoped<LogDbContext>(sp => sp.GetRequiredService<SqliteLogDbContext>());
		_provider = services.BuildServiceProvider();

		await _provider.GetRequiredService<SubmarineDbContext>().Database.EnsureCreatedAsync();
		await _provider.GetRequiredService<LogDbContext>().Database.EnsureCreatedAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _dbConnection.DisposeAsync();
		await _logConnection.DisposeAsync();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldPruneOldCommandsAndLogRows_ButKeepRecentOnes()
	{
		await using var scope = _provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var logDb = scope.ServiceProvider.GetRequiredService<LogDbContext>();
		var now = _clock.GetUtcNow().UtcDateTime;

		db.Commands.Add(new Command { Name = "Old", Status = CommandStatus.COMPLETED });
		db.Commands.Add(new Command { Name = "Recent", Status = CommandStatus.COMPLETED });
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);
		// SubmarineDbContext.TouchTimestamps stamps CreatedAt on insert; backdate it directly to
		// simulate an old command without going through SaveChanges again.
		await db.Commands.Where(x => x.Name == "Old").ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, now.AddDays(-8)), TestContext.Current.CancellationToken);
		await db.Commands.Where(x => x.Name == "Recent").ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, now.AddDays(-1)), TestContext.Current.CancellationToken);

		logDb.Logs.Add(new Log { Time = now.AddDays(-15), Level = "Information", Logger = "x", Message = "old" });
		logDb.Logs.Add(new Log { Time = now.AddDays(-1), Level = "Information", Logger = "x", Message = "recent" });
		await logDb.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new CommandCleanupCommandHandler(db, logDb, _clock);
		await handler.ExecuteAsync(new CommandCleanupCommand(), NullContext.Instance, TestContext.Current.CancellationToken);

		var remainingCommands = await db.Commands.AsNoTracking().Select(x => x.Name).ToListAsync(TestContext.Current.CancellationToken);
		remainingCommands.ShouldBe(["Recent"]);

		var remainingLogs = await logDb.Logs.AsNoTracking().Select(x => x.Message).ToListAsync(TestContext.Current.CancellationToken);
		remainingLogs.ShouldBe(["recent"]);
	}

	private sealed class NullContext : ICommandContext
	{
		public static readonly NullContext Instance = new();

		public int CommandId => 1;

		public Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
	}
}
