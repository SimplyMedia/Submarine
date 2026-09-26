using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Infrastructure.Backups;
using Submarine.Infrastructure.Persistence;
using Xunit;

namespace Submarine.Infrastructure.Tests.Backups;

/// <summary>
///     Exercises the backup service against real temporary Sqlite files.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
	private readonly string _root;
	private readonly string _dbPath;
	private readonly FakeTimeProvider _clock = new();

	public BackupServiceTests()
	{
		_root = Path.Combine(Path.GetTempPath(), $"sub-backup-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_root);
		_dbPath = Path.Combine(_root, "data", "submarine.db");
		Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
		CreateDatabase(_dbPath, connection =>
		{
			Execute(connection, "CREATE TABLE marker (id INTEGER PRIMARY KEY, label TEXT)");
			Execute(connection, "INSERT INTO marker (label) VALUES ('original')");
		});
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();
		Directory.Delete(_root, recursive: true);
	}

	private BackupService CreateService(int retention = 7)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Database:Provider"] = "Sqlite",
				["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}",
				["Backup:Path"] = Path.Combine(_root, "backups"),
				["Backup:Retention"] = retention.ToString()
			})
			.Build();
		return new BackupService(configuration, new DataDirectory(Path.GetDirectoryName(_dbPath)!), _clock, NullLogger<BackupService>.Instance);
	}

	private static void CreateDatabase(string path, Action<SqliteConnection> seed)
	{
		using var connection = new SqliteConnection($"Data Source={path}");
		connection.Open();
		seed(connection);
	}

	private static void Execute(SqliteConnection connection, string text)
	{
		using var command = connection.CreateCommand();
		command.CommandText = text;
		command.ExecuteNonQuery();
	}

	private static int CountRows(string path)
	{
		using var connection = new SqliteConnection($"Data Source={path}");
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM marker";
		return Convert.ToInt32(command.ExecuteScalar());
	}

	[Fact]
	public async Task Create_ShouldWriteValidZipWithSqliteBackup()
	{
		var service = CreateService();

		var entry = await service.CreateAsync(BackupKind.MANUAL, TestContext.Current.CancellationToken);

		entry.Name.ShouldStartWith("submarine_backup_v2_");
		entry.Name.ShouldEndWith("_MANUAL.zip");
		var path = Path.Combine(_root, "backups", entry.Name);
		File.Exists(path).ShouldBeTrue();

		using var archive = ZipFile.OpenRead(path);
		var dbEntry = archive.GetEntry("submarine.db");
		dbEntry.ShouldNotBeNull();
		dbEntry.Length.ShouldBeGreaterThan(0);

		var extracted = Path.Combine(_root, "extracted.db");
		dbEntry.ExtractToFile(extracted);
		CountRows(extracted).ShouldBe(1);
	}

	[Fact]
	public void AppData_ShouldReturnDataDirectory_RegardlessOfProvider()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "Postgres" })
			.Build();
		var service = new BackupService(configuration, new DataDirectory("/config"), _clock, NullLogger<BackupService>.Instance);

		service.AppData().ShouldBe("/config");
	}

	[Fact]
	public async Task Create_ShouldIncludeUserDefinitions_ButNotAppSettingsOrBundled()
	{
		var definitionsDir = Path.Combine(_root, "data", "definitions");
		Directory.CreateDirectory(definitionsDir);
		await File.WriteAllTextAsync(Path.Combine(definitionsDir, "custom.yml"), "id: custom", TestContext.Current.CancellationToken);
		var service = CreateService();

		var entry = await service.CreateAsync(BackupKind.MANUAL, TestContext.Current.CancellationToken);

		using var archive = ZipFile.OpenRead(Path.Combine(_root, "backups", entry.Name));
		archive.GetEntry("definitions/custom.yml").ShouldNotBeNull();
		archive.GetEntry("appsettings.json").ShouldBeNull();
	}

	[Fact]
	public async Task Create_ShouldApplyRetention_KeepingNewest()
	{
		var service = CreateService(retention: 2);

		await service.CreateAsync(BackupKind.SCHEDULED, TestContext.Current.CancellationToken);
		_clock.Advance(TimeSpan.FromSeconds(1));
		await service.CreateAsync(BackupKind.SCHEDULED, TestContext.Current.CancellationToken);
		_clock.Advance(TimeSpan.FromSeconds(1));
		await service.CreateAsync(BackupKind.MANUAL, TestContext.Current.CancellationToken);

		var remaining = service.List();
		remaining.Count.ShouldBe(2);
		remaining.Select(x => x.Name).ShouldContain(n => n.EndsWith("_MANUAL.zip"));
	}

	[Fact]
	public void List_ShouldOnlyContainValidBackupNames()
	{
		var directory = Path.Combine(_root, "backups");
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "submarine_backup_v2_20200101000000_MANUAL.zip"), "fake");
		File.WriteAllText(Path.Combine(directory, "other.zip"), "fake");
		File.WriteAllText(Path.Combine(directory, "../evil.zip"), "fake");

		var service = CreateService();

		service.List().Select(x => x.Name).ShouldBe(["submarine_backup_v2_20200101000000_MANUAL.zip"]);
	}

	[Fact]
	public async Task Delete_ShouldRemoveArchive_AndRejectBadNames()
	{
		var service = CreateService();
		var entry = await service.CreateAsync(BackupKind.MANUAL, TestContext.Current.CancellationToken);

		service.Delete(entry.Name);
		service.List().ShouldBeEmpty();

		Should.Throw<InvalidOperationException>(() => service.Delete("../evil.zip"));
		Should.Throw<KeyNotFoundException>(() => service.Delete(entry.Name));
	}

	[Fact]
	public async Task Restore_ShouldStageValidatedDatabase_WithoutTouchingTheLiveFile()
	{
		var service = CreateService();
		var entry = await service.CreateAsync(BackupKind.MANUAL, TestContext.Current.CancellationToken);

		// Modify the live database after the backup.
		using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
		{
			connection.Open();
			Execute(connection, "INSERT INTO marker (label) VALUES ('changed')");
		}

		CountRows(_dbPath).ShouldBe(2);

		await using var stream = service.Open(entry.Name);
		await service.RestoreAsync(stream, TestContext.Current.CancellationToken);

		// The live database is untouched: connections may still be open against it. The validated
		// backup is staged next to it, to be swapped in on the next application start.
		CountRows(_dbPath).ShouldBe(2);
		var staged = _dbPath + ".restore";
		File.Exists(staged).ShouldBeTrue();
		CountRows(staged).ShouldBe(1);
	}

	[Fact]
	public async Task Restore_ShouldRejectZipWithoutDatabase()
	{
		var service = CreateService();
		var invalidZip = Path.Combine(_root, "invalid.zip");
		using (var archive = ZipFile.Open(invalidZip, ZipArchiveMode.Create))
		{
			var entry = archive.CreateEntry("random.txt");
			await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
			await writer.WriteAsync("no database here".AsMemory(), TestContext.Current.CancellationToken);
		}

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await service.RestoreAsync(File.OpenRead(invalidZip), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Restore_ShouldRejectEntriesThatEscapeTheExtractionDirectory()
	{
		var service = CreateService();
		var maliciousZip = Path.Combine(_root, "malicious.zip");
		using (var archive = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
		{
			// A relative traversal entry name, as a crafted archive would contain.
			var entry = archive.CreateEntry("../../evil.txt");
			using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
			{
				await writer.WriteAsync("payload".AsMemory(), TestContext.Current.CancellationToken);
			}

			archive.CreateEntry("submarine.db");
		}

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await service.RestoreAsync(File.OpenRead(maliciousZip), TestContext.Current.CancellationToken));
		File.Exists(Path.Combine(_root, "..", "..", "evil.txt")).ShouldBeFalse();
	}

	[Fact]
	public async Task Restore_ShouldRejectCorruptDatabase()
	{
		var service = CreateService();
		var corruptZip = Path.Combine(_root, "corrupt.zip");
		using (var archive = ZipFile.Open(corruptZip, ZipArchiveMode.Create))
		{
			var entry = archive.CreateEntry("submarine.db");
			await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
			await writer.WriteAsync("not a real sqlite file".AsMemory(), TestContext.Current.CancellationToken);
		}

		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await service.RestoreAsync(File.OpenRead(corruptZip), TestContext.Current.CancellationToken));
		exception.Message.ShouldContain("not a valid Sqlite database");
		File.Exists(_dbPath + ".restore").ShouldBeFalse();
	}

	[Fact]
	public async Task Open_ShouldRejectUnknownAndInvalidNames()
	{
		var service = CreateService();

		Should.Throw<KeyNotFoundException>(() => service.Open("submarine_backup_v2_20200101000000_MANUAL.zip"));
		Should.Throw<InvalidOperationException>(() => service.Open("../evil.zip"));
		await Task.CompletedTask;
	}
}
