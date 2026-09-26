using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Logging;

/// <summary>
///     Creates standalone LogDbContext instances for the Serilog sink, which runs outside the DI scope.
/// </summary>
public static class LogDbContextFactory
{
	/// <summary>
	///     Create a LogDbContext for the configured provider. Relative Sqlite paths are
	///     resolved against the given base path.
	/// </summary>
	public static LogDbContext Create(IConfiguration configuration, string? basePath = null)
	{
		if (SubmarineDatabase.Provider(configuration) == SubmarineDatabase.Postgres)
		{
			return new PostgresLogDbContext(
				new DbContextOptionsBuilder<PostgresLogDbContext>()
					.UseNpgsql(SubmarineDatabase.PostgresConnectionString(configuration))
					.Options);
		}

		var main = SubmarineDatabase.SqliteConnectionString(configuration);
		if (basePath is not null)
		{
			main = SubmarineDatabase.ResolveSqlitePath(main, basePath);
		}

		var logs = SubmarineDatabase.WithSharedCache(SubmarineDatabase.LogSqliteConnectionString(main));
		return new SqliteLogDbContext(
			new DbContextOptionsBuilder<SqliteLogDbContext>()
				.UseSqlite(logs)
				.AddInterceptors(new SqlitePragmaInterceptor())
				.Options);
	}
}
