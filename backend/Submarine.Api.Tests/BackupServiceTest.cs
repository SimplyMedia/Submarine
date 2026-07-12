using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Exceptions;
using Submarine.Api.Services;
using Xunit;

namespace Submarine.Api.Tests;

public class BackupServiceTest : IDisposable
{
	private readonly string _root;
	private readonly string _backupDir;
	private readonly string _dbPath;

	public BackupServiceTest()
	{
		_root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		_backupDir = Path.Combine(_root, "backups");
		_dbPath = Path.Combine(_root, "submarine.db");
		Directory.CreateDirectory(_root);

		using var connection = new SqliteConnection($"Data Source={_dbPath}");
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "CREATE TABLE Test (Id INTEGER PRIMARY KEY)";
		command.ExecuteNonQuery();
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();

		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	private BackupService CreateService(int? retention = null, string provider = "Sqlite")
	{
		var settings = new Dictionary<string, string?>
		{
			["Backup:Path"] = _backupDir,
			["Database:Provider"] = provider,
			["ConnectionStrings:SqliteConnection"] = $"Data Source={_dbPath}"
		};

		if (retention != null)
			settings["Backup:Retention"] = retention.Value.ToString();

		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new BackupService(configuration, NullLogger<BackupService>.Instance);
	}

	[Fact]
	public async Task CreateAsync_ShouldZipDatabase_WhenProviderIsSqlite()
	{
		var service = CreateService();

		var entry = await service.CreateAsync();

		var path = Path.Combine(_backupDir, entry.Name);
		Assert.True(File.Exists(path));
		Assert.Equal(new FileInfo(path).Length, entry.Size);

		using var archive = ZipFile.OpenRead(path);
		Assert.Contains(archive.Entries, e => e.Name == "submarine.db");
	}

	[Fact]
	public async Task CreateAsync_ShouldWriteWarningInsteadOfDatabase_WhenProviderIsPostgres()
	{
		var service = CreateService(provider: "Postgres");

		var entry = await service.CreateAsync();

		using var archive = ZipFile.OpenRead(Path.Combine(_backupDir, entry.Name));
		Assert.Contains(archive.Entries, e => e.Name == "README.txt");
		Assert.DoesNotContain(archive.Entries, e => e.Name == "submarine.db");
	}

	[Fact]
	public async Task List_ShouldReturnCreatedBackup()
	{
		var service = CreateService();

		var entry = await service.CreateAsync();
		var list = service.List();

		Assert.Single(list);
		Assert.Equal(entry.Name, list[0].Name);
	}

	[Fact]
	public async Task CreateAsync_ShouldDeleteOldestBackups_WhenExceedingRetention()
	{
		var service = CreateService(retention: 3);
		Directory.CreateDirectory(_backupDir);

		for (var i = 0; i < 4; i++)
		{
			var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10 + i);
			var path = Path.Combine(_backupDir, $"submarine_backup_{timestamp:yyyyMMddHHmmss}.zip");
			File.WriteAllText(path, "fake");
			File.SetCreationTimeUtc(path, timestamp.UtcDateTime);
		}

		await service.CreateAsync();

		Assert.Equal(3, service.List().Count);
	}

	[Fact]
	public void ResolvePath_ShouldThrowBadRequest_WhenNameAttemptsPathTraversal()
	{
		var service = CreateService();

		Assert.Throws<BadRequestException>(() => service.ResolvePath("../../etc/passwd"));
	}

	[Fact]
	public void ResolvePath_ShouldThrowBadRequest_WhenNameDoesNotMatchBackupPattern()
	{
		var service = CreateService();

		Assert.Throws<BadRequestException>(() => service.ResolvePath("not-a-backup.zip"));
	}

	[Fact]
	public void ResolvePath_ShouldThrowNotFound_WhenBackupDoesNotExist()
	{
		var service = CreateService();

		Assert.Throws<NotFoundException>(() => service.ResolvePath("submarine_backup_20200101120000.zip"));
	}
}
