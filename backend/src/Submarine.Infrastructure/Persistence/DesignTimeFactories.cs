using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Design time factory for the Sqlite main context, used by dotnet-ef.
/// </summary>
public sealed class SqliteSubmarineDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SqliteSubmarineDbContext>
{
	/// <inheritdoc />
	public SqliteSubmarineDbContext CreateDbContext(string[] args)
		=> new(
			new DbContextOptionsBuilder<SqliteSubmarineDbContext>().UseSqlite("Data Source=submarine-design.db").Options,
			TimeProvider.System);
}

/// <summary>
///     Design time factory for the Postgres main context, used by dotnet-ef.
/// </summary>
public sealed class PostgresSubmarineDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PostgresSubmarineDbContext>
{
	/// <inheritdoc />
	public PostgresSubmarineDbContext CreateDbContext(string[] args)
		=> new(
			new DbContextOptionsBuilder<PostgresSubmarineDbContext>()
				.UseNpgsql("Host=localhost;Database=submarine-design;Username=postgres;Password=postgres")
				.Options,
			TimeProvider.System);
}

/// <summary>
///     Design time factory for the Sqlite log context, used by dotnet-ef.
/// </summary>
public sealed class SqliteLogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SqliteLogDbContext>
{
	/// <inheritdoc />
	public SqliteLogDbContext CreateDbContext(string[] args)
		=> new(new DbContextOptionsBuilder<SqliteLogDbContext>().UseSqlite("Data Source=submarine-logs-design.db").Options);
}

/// <summary>
///     Design time factory for the Postgres log context, used by dotnet-ef.
/// </summary>
public sealed class PostgresLogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PostgresLogDbContext>
{
	/// <inheritdoc />
	public PostgresLogDbContext CreateDbContext(string[] args)
		=> new(new DbContextOptionsBuilder<PostgresLogDbContext>()
			.UseNpgsql("Host=localhost;Database=submarine-design;Username=postgres;Password=postgres")
			.Options);
}
