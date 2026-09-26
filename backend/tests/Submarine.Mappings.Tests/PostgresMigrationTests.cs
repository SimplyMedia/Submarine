using Microsoft.EntityFrameworkCore;
using Submarine.Mappings.Data;
using Submarine.Mappings.Entities;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Submarine.Mappings.Tests;

public sealed class PostgresMigrationTests
{
	[Fact]
	public async Task Migrate_ShouldApplyInitialMigration_WhenPostgresIsAvailable()
	{
		var container = new PostgreSqlBuilder("postgres:17-alpine")
			.Build();
		try
		{
			await container.StartAsync();
		}
		catch (Exception exception)
		{
			await container.DisposeAsync();
			Assert.Skip($"Docker is not available, skipping PostgreSQL migration test: {exception.Message}");
		}

		await using (container)
		{
			await PostgresReadiness.WaitAsync(container.GetConnectionString());
			var options = new DbContextOptionsBuilder<PostgresMappingsDbContext>()
				.UseNpgsql(container.GetConnectionString())
				.Options;
			await using var db = new PostgresMappingsDbContext(options);

			await db.Database.MigrateAsync();

			db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = null, EpisodeOffset = 0 });
			await db.SaveChangesAsync();

			var stored = await db.SceneMappings.SingleAsync();
			stored.TvdbId.ShouldBe(1);
			stored.Title.ShouldBe("Show");

			// Wildcard uniqueness: a second row for the same series and season must fail on the unique index.
			db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Duplicate", SeasonNumber = 1, SceneSeasonNumber = null, EpisodeOffset = 0 });
			await Should.ThrowAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
		}
	}
}
