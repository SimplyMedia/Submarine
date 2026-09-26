using Npgsql;

namespace Submarine.Api.IntegrationTests;

/// <summary>
///     The Postgres image restarts once after initdb, so the mapped port can accept a connection that the
///     server then drops. Tests wait for a working query before using the container.
/// </summary>
internal static class PostgresReadiness
{
	public static async Task WaitAsync(string connectionString)
	{
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (true)
		{
			try
			{
				await using var connection = new NpgsqlConnection(connectionString);
				await connection.OpenAsync();
				await using var command = new NpgsqlCommand("select 1", connection);
				await command.ExecuteScalarAsync();
				return;
			}
			catch (NpgsqlException) when (DateTime.UtcNow < deadline)
			{
				await Task.Delay(250);
			}
		}
	}
}
