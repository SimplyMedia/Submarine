using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Registers both contexts for the configured provider, the startup migrator and the seeder.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
	/// <summary>
	///     Add Submarine persistence. Reads Database:Provider (Sqlite default, Postgres optional).
	/// </summary>
	/// <param name="services">Service collection.</param>
	/// <param name="configuration">Application configuration.</param>
	/// <param name="basePath">Base path used to resolve relative Sqlite paths, usually the content root.</param>
	public static IServiceCollection AddSubmarinePersistence(
		this IServiceCollection services,
		IConfiguration configuration,
		string? basePath = null)
	{
		services.TryAddSingleton(TimeProvider.System);

		if (SubmarineDatabase.Provider(configuration) == SubmarineDatabase.Postgres)
		{
			var connectionString = SubmarineDatabase.PostgresConnectionString(configuration);
			services.AddDbContext<PostgresSubmarineDbContext>(options =>
				options.UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
			services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<PostgresSubmarineDbContext>());
			services.AddDbContext<PostgresLogDbContext>(options => options.UseNpgsql(connectionString));
			services.AddScoped<LogDbContext>(sp => sp.GetRequiredService<PostgresLogDbContext>());
			services.AddScoped<SubmarineSeeder>();
			services.AddHostedService<SubmarineMigrationService>();
			return services;
		}

		var sqlite = SubmarineDatabase.WithSharedCache(SubmarineDatabase.SqliteConnectionString(configuration));
		if (basePath is not null)
		{
			sqlite = SubmarineDatabase.ResolveSqlitePath(sqlite, basePath);
		}

		SubmarineDatabase.EnsureSqliteDirectory(sqlite);

		var logs = SubmarineDatabase.WithSharedCache(SubmarineDatabase.LogSqliteConnectionString(sqlite));
		SubmarineDatabase.EnsureSqliteDirectory(logs);

		services.AddDbContext<SqliteSubmarineDbContext>(options =>
			options.UseSqlite(sqlite, sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
				.AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<SubmarineDbContext>(sp => sp.GetRequiredService<SqliteSubmarineDbContext>());
		services.AddDbContext<SqliteLogDbContext>(options =>
			options.UseSqlite(logs).AddInterceptors(new SqlitePragmaInterceptor()));
		services.AddScoped<LogDbContext>(sp => sp.GetRequiredService<SqliteLogDbContext>());

		services.AddScoped<SubmarineSeeder>();
		services.AddHostedService<SubmarineMigrationService>();

		return services;
	}
}
