using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Tests.Import;

/// <summary>
///     Builds an in-memory Sqlite backed <see cref="SqliteSubmarineDbContext" /> for tests. The connection is kept
///     open for the lifetime of the context so the in-memory database survives across scopes.
/// </summary>
public static class TestDbFactory
{
	/// <summary>
	///     Create a fresh in-memory database with the schema applied.
	/// </summary>
	public static SqliteSubmarineDbContext Create(TimeProvider timeProvider)
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();

		var options = new DbContextOptionsBuilder<SqliteSubmarineDbContext>()
			.UseSqlite(connection)
			.Options;

		var db = new SqliteSubmarineDbContext(options, timeProvider);
		db.Database.EnsureCreated();
		return db;
	}
}
