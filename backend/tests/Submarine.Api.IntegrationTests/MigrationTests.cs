using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Submarine.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Submarine.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Submarine.Api.IntegrationTests;

public sealed class SqliteMigrationTests : IClassFixture<SubmarineApiFactory>
{
	private readonly SubmarineApiFactory _factory;

	public SqliteMigrationTests(SubmarineApiFactory factory) => _factory = factory;

	[Fact]
	public void Migrations_ShouldApply_FromScratch_OnSqlite()
	{
		// Creating a client starts the host and runs the factory migration step.
		_factory.CreateClient().Dispose();

		File.Exists(_factory.DbPath).ShouldBeTrue("the API should have migrated its database on startup");

		using var connection = new SqliteConnection($"Data Source={_factory.DbPath}");
		connection.Open();

		var applied = Count(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory");
		applied.ShouldBeGreaterThanOrEqualTo(1);

		Count(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'").ShouldBe(1);
		Count(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='GeneralConfig'").ShouldBe(1);
		Count(connection, "SELECT COUNT(*) FROM GeneralConfig").ShouldBe(1);
		Count(connection, "SELECT COUNT(*) FROM QualityDefinitions").ShouldBeGreaterThan(0);
		Count(connection, "SELECT COUNT(*) FROM ScheduledTasks").ShouldBe(9);
		Count(connection, "SELECT COUNT(*) FROM Users").ShouldBe(0);

		static int Count(SqliteConnection connection, string sql)
		{
			using var command = connection.CreateCommand();
			command.CommandText = sql;
			return Convert.ToInt32(command.ExecuteScalar());
		}
	}

	[Fact]
	public async Task Migrations_ShouldBackfillRemoveCompletedAndFailed_ForExistingDownloadClientRows()
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"submarine-parity-migrate-{Guid.NewGuid():N}.db");
		try
		{
			var options = new DbContextOptionsBuilder<SqliteSubmarineDbContext>()
				.UseSqlite($"Data Source={dbPath}")
				.Options;

			await using (var db = new SqliteSubmarineDbContext(options, TimeProvider.System))
			{
				await db.GetService<IMigrator>().MigrateAsync("20260926124842_Initial");
			}

			using (var connection = new SqliteConnection($"Data Source={dbPath}"))
			{
				connection.Open();
				using var insert = connection.CreateCommand();
				insert.CommandText = """
					INSERT INTO DownloadClients (Name, Type, Enable, Priority, SettingsJson, RemoveCompleted, RemoveFailed, CreatedAt, UpdatedAt)
					VALUES ('Legacy', 0, 1, 1, '{}', 0, 0, '2024-01-01 00:00:00', '2024-01-01 00:00:00');
					""";
				insert.ExecuteNonQuery();
			}

			await using (var db = new SqliteSubmarineDbContext(options, TimeProvider.System))
			{
				await db.GetService<IMigrator>().MigrateAsync();
			}

			using (var connection = new SqliteConnection($"Data Source={dbPath}"))
			{
				connection.Open();
				using var query = connection.CreateCommand();
				query.CommandText = "SELECT RemoveCompleted, RemoveFailed FROM DownloadClients WHERE Name = 'Legacy'";
				using var reader = query.ExecuteReader();

				reader.Read().ShouldBeTrue();
				Convert.ToBoolean(reader.GetInt64(0)).ShouldBeTrue("an existing client must be backfilled to remove completed downloads");
				Convert.ToBoolean(reader.GetInt64(1)).ShouldBeTrue("an existing client must be backfilled to remove failed downloads");
			}
		}
		finally
		{
			if (File.Exists(dbPath))
				File.Delete(dbPath);
		}
	}

	[Fact]
	public async Task LogsDatabase_ShouldBeMigrated_ForTheSink()
	{
		var settings = new Dictionary<string, string?>
		{
			["Database:Provider"] = "Sqlite",
			["ConnectionStrings:Sqlite"] = $"Data Source={_factory.DbPath}"
		};
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		using var db = LogDbContextFactory.Create(configuration);
		await db.Database.MigrateAsync();
		db.Logs.Count().ShouldBeGreaterThanOrEqualTo(0);
	}
}

/// <summary>
///     Verifies the Postgres migration path using a throwaway container.
///     Skipped when Docker is not reachable.
/// </summary>
public sealed class PostgresMigrationTests
{
	[Fact]
	public async Task Migrations_ShouldApply_FromScratch_OnPostgres()
	{
		if (!DockerAvailable())
		{
			Assert.Skip("Docker is not available, skipping Postgres migration test");
		}

		var container = new PostgreSqlBuilder("postgres:17-alpine")
			.WithDatabase("submarine")
			.WithUsername("submarine")
			.WithPassword("submarine")
			.Build();
		await container.StartAsync();
		try
		{
			await PostgresReadiness.WaitAsync(container.GetConnectionString());
			await using var factory = new SubmarineApiFactory
			{
				PostgresConnectionString = container.GetConnectionString()
			};
			var client = factory.CreateClient();

			// Ready implies both contexts migrated successfully, otherwise startup fails.
			(await client.GetAsync("/_status/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
			(await client.GetAsync("/_status/healthz")).StatusCode.ShouldBe(HttpStatusCode.OK);
		}
		finally
		{
			await container.DisposeAsync();
		}
	}

	[Fact]
	public async Task Setup_ShouldBeAtomic_OnPostgres_WhenRequestsRaceForTheFirstUser()
	{
		if (!DockerAvailable())
		{
			Assert.Skip("Docker is not available, skipping Postgres setup race test");
		}

		var container = new PostgreSqlBuilder("postgres:17-alpine")
			.WithDatabase("submarine")
			.WithUsername("submarine")
			.WithPassword("submarine")
			.Build();
		await container.StartAsync();
		try
		{
			await PostgresReadiness.WaitAsync(container.GetConnectionString());
			await using var factory = new SubmarineApiFactory
			{
				PostgresConnectionString = container.GetConnectionString()
			};

			// Serialization failures (40001) on the losing transactions must map to 409, not 500.
			var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => factory.CreateClient()
				.PostAsJsonAsync("/api/v1/setup", new { username = $"admin{i}", password = "correct-horse" })));

			responses.Select(r => r.StatusCode).Order().ShouldBe([HttpStatusCode.Created, .. Enumerable.Repeat(HttpStatusCode.Conflict, 5)]);
			(await factory.WithDbAsync(db => db.Users.CountAsync())).ShouldBe(1);
		}
		finally
		{
			await container.DisposeAsync();
		}
	}

	private static bool DockerAvailable()
		=> File.Exists("/var/run/docker.sock")
			|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST"));
}
