using Microsoft.EntityFrameworkCore;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Persistence.Converters;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Database holding application logs. On Sqlite this lives in its own logs.db,
///     on Postgres in the same database in a separate table.
/// </summary>
public class LogDbContext(DbContextOptions options) : DbContext(options)
{
	/// <summary>Log rows.</summary>
	public DbSet<Log> Logs => Set<Log>();

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Log>(builder =>
		{
			builder.HasKey(x => x.Id);
			builder.HasIndex(x => x.Time);
		});
	}

	/// <inheritdoc />
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
	}
}

/// <summary>
///     Log database bound to Sqlite.
/// </summary>
public sealed class SqliteLogDbContext(DbContextOptions<SqliteLogDbContext> options) : LogDbContext(options);

/// <summary>
///     Log database bound to Postgres.
/// </summary>
public sealed class PostgresLogDbContext(DbContextOptions<PostgresLogDbContext> options) : LogDbContext(options);
