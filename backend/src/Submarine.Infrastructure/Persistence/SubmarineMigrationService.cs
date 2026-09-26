using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Runs migrations and seeds before the application serves traffic.
/// </summary>
public sealed class SubmarineMigrationService(IServiceScopeFactory scopeFactory) : IHostedService
{
	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (SubmarineDesignTime.IsDocumentGeneration())
		{
			// Build time OpenAPI generation has no migrated database.
			return;
		}

		await using var scope = scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<SubmarineDbContext>();

		// Without generated migrations there is nothing to apply and seeding would fail.
		if (!db.Database.GetMigrations().Any())
		{
			return;
		}

		await SubmarineInitializer.InitializeAsync(scope.ServiceProvider, cancellationToken);
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
