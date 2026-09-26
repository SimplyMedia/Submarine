using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Auth;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Auth;

public sealed class AuthConfigProviderTests : IAsyncLifetime
{
	private SqliteConnection _connection = null!;
	private ServiceProvider _services = null!;

	public async ValueTask InitializeAsync()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		var services = new ServiceCollection();
		services.AddSingleton(TimeProvider.System);
		services.AddMemoryCache();
		services.AddDbContext<SqliteSubmarineDbContext>(options => options.UseSqlite(_connection));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.AddSingleton<LoggingLevelSwitch>();
		_services = services.BuildServiceProvider();

		var db = _services.GetRequiredService<SubmarineDbContext>();
		await db.Database.EnsureCreatedAsync();
		db.GeneralConfig.Add(new GeneralConfig { LogLevel = "Warning" });
		await db.SaveChangesAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _services.DisposeAsync();
		await _connection.DisposeAsync();
	}

	[Fact]
	public async Task GetSnapshotAsync_AppliesChangedLogLevelAfterInvalidation()
	{
		var logLevelSwitch = _services.GetRequiredService<LoggingLevelSwitch>();
		var provider = new AuthConfigProvider(
			_services.GetRequiredService<IMemoryCache>(),
			_services.GetRequiredService<IServiceScopeFactory>(),
			logLevelSwitch);

		await provider.GetSnapshotAsync(TestContext.Current.CancellationToken);
		logLevelSwitch.MinimumLevel.ShouldBe(LogEventLevel.Warning);

		var db = _services.GetRequiredService<SubmarineDbContext>();
		var config = await db.GeneralConfig.SingleAsync(TestContext.Current.CancellationToken);
		config.LogLevel = "Debug";
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);
		provider.Invalidate();

		await provider.GetSnapshotAsync(TestContext.Current.CancellationToken);
		logLevelSwitch.MinimumLevel.ShouldBe(LogEventLevel.Debug);
	}
}
