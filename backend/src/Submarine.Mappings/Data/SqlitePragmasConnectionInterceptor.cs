using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Submarine.Mappings.Data;

/// <summary>
/// Applies the SQLite pragmas the service relies on to every opened connection.
/// </summary>
public sealed class SqlitePragmasConnectionInterceptor : DbConnectionInterceptor
{
	public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
	{
		if (connection is SqliteConnection sqliteConnection)
		{
			await using var command = sqliteConnection.CreateCommand();
			command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
	}
}
