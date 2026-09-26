using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Reads database provider and connection strings from configuration and normalizes Sqlite paths.
/// </summary>
public static class SubmarineDatabase
{
	/// <summary>Sqlite provider name.</summary>
	public const string Sqlite = "Sqlite";

	/// <summary>Postgres provider name.</summary>
	public const string Postgres = "Postgres";

	/// <summary>
	///     The configured provider. Sqlite is the default, Postgres is the only alternative.
	/// </summary>
	public static string Provider(IConfiguration configuration)
	{
		var value = configuration["Database:Provider"];
		return string.Equals(value, Postgres, StringComparison.OrdinalIgnoreCase) ? Postgres : Sqlite;
	}

	/// <summary>
	///     Sqlite main database connection string.
	/// </summary>
	public static string SqliteConnectionString(IConfiguration configuration)
		=> configuration.GetConnectionString("Sqlite") ?? "Data Source=data/submarine.db";

	/// <summary>
	///     Postgres connection string.
	/// </summary>
	public static string PostgresConnectionString(IConfiguration configuration)
		=> configuration.GetConnectionString("Postgres")
			?? throw new InvalidOperationException("ConnectionStrings:Postgres is required when Database:Provider is Postgres");

	/// <summary>
	///     Sqlite log database connection string, stored next to the main database.
	/// </summary>
	public static string LogSqliteConnectionString(string mainSqliteConnectionString)
	{
		var builder = new SqliteConnectionStringBuilder(mainSqliteConnectionString);
		var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
		builder.DataSource = Path.Combine(directory ?? ".", "logs.db");
		return builder.ToString();
	}

	/// <summary>
	///     Resolves a relative Data Source against the given base path.
	/// </summary>
	public static string ResolveSqlitePath(string connectionString, string basePath)
	{
		var builder = new SqliteConnectionStringBuilder(connectionString);
		if (string.IsNullOrEmpty(builder.DataSource)
			|| builder.DataSource == ":memory:"
			|| Path.IsPathRooted(builder.DataSource))
		{
			return builder.ToString();
		}

		builder.DataSource = Path.GetFullPath(Path.Combine(basePath, builder.DataSource));
		return builder.ToString();
	}

	/// <summary>
	///     Creates the directory holding the database file, if needed.
	/// </summary>
	public static void EnsureSqliteDirectory(string connectionString)
	{
		var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
		if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:")
		{
			return;
		}

		var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}
	}

	/// <summary>
	///     Enables the shared cache so multiple connections see one database instance.
	/// </summary>
	public static string WithSharedCache(string connectionString)
	{
		var builder = new SqliteConnectionStringBuilder(connectionString) { Cache = SqliteCacheMode.Shared };
		return builder.ConnectionString;
	}
}
