using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Applies migrations for both contexts and seeds defaults. Safe to call repeatedly.
/// </summary>
public static class SubmarineInitializer
{
	/// <summary>
	///     Migrate the main and log database, then seed defaults.
	/// </summary>
	public static async Task InitializeAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken = default)
	{
		var db = scopedProvider.GetRequiredService<SubmarineDbContext>();
		await db.Database.MigrateAsync(cancellationToken);

		var logDb = scopedProvider.GetRequiredService<LogDbContext>();
		await logDb.Database.MigrateAsync(cancellationToken);

		await scopedProvider.GetRequiredService<SubmarineSeeder>().SeedAsync(cancellationToken);
	}
}
