using Microsoft.EntityFrameworkCore;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Main database bound to Sqlite.
/// </summary>
public sealed class SqliteSubmarineDbContext(DbContextOptions<SqliteSubmarineDbContext> options, TimeProvider timeProvider)
	: SubmarineDbContext(options, timeProvider);

/// <summary>
///     Main database bound to Postgres.
/// </summary>
public sealed class PostgresSubmarineDbContext(DbContextOptions<PostgresSubmarineDbContext> options, TimeProvider timeProvider)
	: SubmarineDbContext(options, timeProvider);
