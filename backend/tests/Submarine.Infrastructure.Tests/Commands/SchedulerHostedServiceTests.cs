using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Commands;

/// <summary>
///     Regression coverage for B-F18: a scheduled task with a 0 minute interval (the "disabled"
///     sentinel documented on <see cref="IndexerConfig.RssSyncIntervalMinutes" /> and
///     <see cref="Submarine.Core.Entities.DownloadConfig.CheckForFinishedDownloadInterval" />) must
///     never be picked up by the scheduler tick, regardless of its NextRun value.
/// </summary>
public sealed class SchedulerHostedServiceTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;
	private readonly FakeTimeProvider _clock = new();

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();

		var services = new ServiceCollection();
		services.TryAddSingleton<TimeProvider>(_clock);
		services.TryAddSingleton(Substitute.For<IEventBus>());
		services.AddDbContext<SqliteSubmarineDbContext>(options =>
			options.UseSqlite(_connection).AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.TryAddSingleton<CommandRegistry>();
		services.TryAddSingleton<CommandExecutionRegistry>();
		services.TryAddSingleton<CommandQueue>();
		services.TryAddSingleton<ICommandQueue>(sp => sp.GetRequiredService<CommandQueue>());

		_provider = services.BuildServiceProvider();
		var db = _provider.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
		db.ScheduledTasks.AddRange(
			new ScheduledTask { Name = "RssSync", IntervalMinutes = 0, NextRun = null },
			new ScheduledTask { Name = "DownloadMonitor", IntervalMinutes = 30, NextRun = null });
		await db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	[Fact]
	public async Task Tick_ShouldSkipDisabledTask_AndRunEnabledTask()
	{
		var service = new SchedulerHostedService(
			_provider.GetRequiredService<IServiceScopeFactory>(),
			_provider.GetRequiredService<ICommandQueue>(),
			_provider.GetRequiredService<CommandRegistry>(),
			_clock,
			NullLogger<SchedulerHostedService>.Instance);

		var tickAsync = typeof(SchedulerHostedService).GetMethod("TickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
		await (Task)tickAsync.Invoke(service, [TestContext.Current.CancellationToken])!;

		using var scope = _provider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var rssSync = await db.ScheduledTasks.SingleAsync(x => x.Name == "RssSync", TestContext.Current.CancellationToken);
		var downloadMonitor = await db.ScheduledTasks.SingleAsync(x => x.Name == "DownloadMonitor", TestContext.Current.CancellationToken);

		rssSync.LastRun.ShouldBeNull();
		rssSync.NextRun.ShouldBeNull();
		downloadMonitor.LastRun.ShouldNotBeNull();
		downloadMonitor.NextRun.ShouldNotBeNull();

		(await db.Commands.CountAsync(x => x.Name == "RssSync", TestContext.Current.CancellationToken)).ShouldBe(0);
		(await db.Commands.CountAsync(x => x.Name == "DownloadMonitor", TestContext.Current.CancellationToken)).ShouldBe(1);
	}
}
