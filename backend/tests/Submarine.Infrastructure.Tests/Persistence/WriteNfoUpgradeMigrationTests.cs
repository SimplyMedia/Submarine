using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Persistence;

/// <summary>
///     The MetadataConsumers migration must preserve NFO writing for installs that had WriteNfo enabled,
///     since FileDate (which replaces the WriteNfo column) is an unrelated setting.
/// </summary>
public sealed class WriteNfoUpgradeMigrationTests : IDisposable
{
	private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"submarine-writenfo-upgrade-{Guid.NewGuid():N}.db");

	public void Dispose() => File.Delete(_dbPath);

	[Fact]
	public void MetadataConsumers_ShouldInsertEnabledKodiConsumer_WhenWriteNfoWasTrue()
	{
		using (var context = CreateContext())
		{
			var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();

			// Stop at the pre-parity schema, where MediaManagementConfig still has the WriteNfo column.
			migrator.Migrate("20260926124842_Initial");

			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();
			using (var insert = connection.CreateCommand())
			{
				insert.CommandText = """
					INSERT INTO MediaManagementConfig
						(Id, UseHardlinks, ImportExtraFiles, ExtraFileExtensions, MinimumFreeSpaceMb, SkipFreeSpaceCheck,
						 WriteNfo, RecycleBinPath, RecycleBinCleanupDays, CreateEmptySeriesFolders, CreateEmptyMovieFolders,
						 DeleteEmptyFolders, UnmonitorDeletedFiles, ChmodFolder, ChmodFile, ChownGroup,
						 DownloadPropersAndRepacks, EnableMediaInfo, UpdatedAt)
					VALUES
						(1, 1, 0, 'nfo', 100, 0, 1, '', 7, 0, 0, 0, 0, '', '', '', 0, 1, '2026-01-01');
					""";
				insert.ExecuteNonQuery();
			}

			// Apply the remaining migrations, including MetadataConsumers.
			migrator.Migrate();
		}

		using var verifyContext = CreateContext();
		var consumer = verifyContext.MetadataConsumers.AsNoTracking().Single();

		consumer.Name.ShouldBe("Kodi (XBMC) / Emby");
		consumer.Type.ShouldBe(MetadataConsumerType.KODI);
		consumer.SettingsJson.ShouldBe("{}");
		consumer.Enable.ShouldBeTrue();
	}

	[Fact]
	public void MetadataConsumers_ShouldNotInsertConsumer_WhenWriteNfoWasFalse()
	{
		using (var context = CreateContext())
		{
			var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
			migrator.Migrate("20260926124842_Initial");

			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();
			using (var insert = connection.CreateCommand())
			{
				insert.CommandText = """
					INSERT INTO MediaManagementConfig
						(Id, UseHardlinks, ImportExtraFiles, ExtraFileExtensions, MinimumFreeSpaceMb, SkipFreeSpaceCheck,
						 WriteNfo, RecycleBinPath, RecycleBinCleanupDays, CreateEmptySeriesFolders, CreateEmptyMovieFolders,
						 DeleteEmptyFolders, UnmonitorDeletedFiles, ChmodFolder, ChmodFile, ChownGroup,
						 DownloadPropersAndRepacks, EnableMediaInfo, UpdatedAt)
					VALUES
						(1, 1, 0, 'nfo', 100, 0, 0, '', 7, 0, 0, 0, 0, '', '', '', 0, 1, '2026-01-01');
					""";
				insert.ExecuteNonQuery();
			}

			migrator.Migrate();
		}

		using var verifyContext = CreateContext();
		verifyContext.MetadataConsumers.AsNoTracking().ShouldBeEmpty();
	}

	private SqliteSubmarineDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<SqliteSubmarineDbContext>()
			.UseSqlite($"Data Source={_dbPath}")
			.Options;
		return new SqliteSubmarineDbContext(options, new FakeTimeProvider());
	}
}
