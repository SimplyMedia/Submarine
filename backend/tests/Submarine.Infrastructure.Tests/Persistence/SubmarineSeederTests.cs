using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Persistence;

/// <summary>
///     Asserts the default scheduled task seeding behavior.
/// </summary>
public sealed class SubmarineSeederTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _provider = null!;
	private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(_clock);
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.AddScoped<SubmarineSeeder>();
		_provider = services.BuildServiceProvider();
		await _provider.GetRequiredService<SubmarineDbContext>().Database.EnsureCreatedAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _provider.DisposeAsync();
		await _connection.DisposeAsync();
	}

	[Fact]
	public async Task SeedAsync_ShouldScheduleHealthCheckAndDefinitionSync_ToRunImmediately()
	{
		await using var scope = _provider.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<SubmarineSeeder>().SeedAsync(TestContext.Current.CancellationToken);

		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();
		var tasks = await db.ScheduledTasks.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
		var now = _clock.GetUtcNow().UtcDateTime;

		tasks.Single(x => x.Name == "HealthCheck").NextRun.ShouldBe(now);
		tasks.Single(x => x.Name == "IndexerDefinitionSync").NextRun.ShouldBe(now);
		tasks.Single(x => x.Name == "RssSync").NextRun.ShouldBe(now.AddMinutes(30));
	}
}
