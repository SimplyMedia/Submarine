using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Runs WAL journal mode and a busy timeout on every opened Sqlite connection.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
	/// <inheritdoc />
	public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
	{
		if (connection is SqliteConnection sqlite)
		{
			Apply(sqlite);
		}

		return result;
	}

	/// <inheritdoc />
	public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
		DbConnection connection,
		ConnectionEventData eventData,
		InterceptionResult result,
		CancellationToken cancellationToken = default)
	{
		if (connection is SqliteConnection sqlite)
		{
			await ApplyAsync(sqlite, cancellationToken);
		}

		return result;
	}

	private static void Apply(SqliteConnection connection)
	{
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
		command.ExecuteNonQuery();
	}

	private static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
