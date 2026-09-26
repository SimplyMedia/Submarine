using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Infrastructure.Backups;

/// <summary>
///     Kind of a backup.
/// </summary>
public enum BackupKind
{
	/// <summary>Requested through the API.</summary>
	MANUAL,

	/// <summary>Created by the scheduler.</summary>
	SCHEDULED
}

/// <summary>
///     One backup archive on disk.
/// </summary>
/// <param name="Name">File name including extension.</param>
/// <param name="Size">Archive size in bytes.</param>
/// <param name="CreatedAt">UTC timestamp of the archive file.</param>
/// <param name="Kind">Whether the backup was requested manually or created by the scheduler.</param>
public sealed record BackupEntry(string Name, long Size, DateTime CreatedAt, BackupKind Kind);

/// <summary>
///     Creates, lists, deletes and restores database backups as zip archives.
/// </summary>
public sealed partial class BackupService(
	IConfiguration configuration,
	DataDirectory dataDirectory,
	TimeProvider timeProvider,
	ILogger<BackupService> logger)
{
	/// <summary>Matches exactly the backup file names this service creates.</summary>
	[GeneratedRegex(@"^submarine_backup_v2_\d{14}_(MANUAL|SCHEDULED)\.zip$")]
	public static partial Regex FileNamePattern();

	/// <summary>
	///     Creates a backup archive and applies retention.
	/// </summary>
	/// <param name="kind">Whether the backup was requested manually or by the scheduler.</param>
	/// <param name="folder">The configured backup folder; empty uses "backups" under the app data directory.</param>
	/// <param name="retention">How many of the most recent backups to keep.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task<BackupEntry> CreateAsync(BackupKind kind, string folder, int retention, CancellationToken cancellationToken = default)
	{
		var directory = BackupDirectory(folder);
		Directory.CreateDirectory(directory);
		var timestamp = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		var fileName = $"submarine_backup_v2_{timestamp}_{kind}.zip";
		var targetPath = Path.Combine(directory, fileName);
		var tempPath = targetPath + ".tmp";

		try
		{
			await using (var stream = new FileStream(tempPath, FileMode.Create))
			{
				using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
				await AddDatabaseAsync(archive, cancellationToken);
				await AddDefinitionsAsync(archive, cancellationToken);
			}

			File.Move(tempPath, targetPath, overwrite: true);
		}
		finally
		{
			File.Delete(tempPath);
		}

		ApplyRetention(folder, retention);
		var info = new FileInfo(targetPath);
		return new BackupEntry(fileName, info.Length, info.CreationTimeUtc, kind);
	}

	/// <summary>
	///     Lists all valid backup archives.
	/// </summary>
	/// <param name="folder">The configured backup folder; empty uses "backups" under the app data directory.</param>
	public IReadOnlyList<BackupEntry> List(string folder)
	{
		var directory = BackupDirectory(folder);
		if (!Directory.Exists(directory))
		{
			return [];
		}

		return Directory.EnumerateFiles(directory, "*.zip")
			.Select(Path.GetFileName)
			.Where(name => name is not null && FileNamePattern().IsMatch(name!))
			.Select(name =>
			{
				var info = new FileInfo(Path.Combine(directory, name!));
				var kind = Enum.Parse<BackupKind>(FileNamePattern().Match(name!).Groups[1].Value);
				return new BackupEntry(name!, info.Length, info.CreationTimeUtc, kind);
			})
			.OrderByDescending(entry => entry.CreatedAt)
			.ToList();
	}

	/// <summary>
	///     Deletes a backup archive.
	/// </summary>
	/// <param name="name">The archive file name.</param>
	/// <param name="folder">The configured backup folder; empty uses "backups" under the app data directory.</param>
	/// <exception cref="KeyNotFoundException">The archive does not exist.</exception>
	/// <exception cref="InvalidOperationException">The name does not match the backup pattern.</exception>
	public void Delete(string name, string folder)
	{
		var path = GuardedPath(name, folder);
		if (!File.Exists(path))
		{
			throw new KeyNotFoundException($"Backup {name} does not exist");
		}

		File.Delete(path);
	}

	/// <summary>
	///     Opens a backup archive for download.
	/// </summary>
	/// <param name="name">The archive file name.</param>
	/// <param name="folder">The configured backup folder; empty uses "backups" under the app data directory.</param>
	/// <exception cref="KeyNotFoundException">The archive does not exist.</exception>
	/// <exception cref="InvalidOperationException">The name does not match the backup pattern.</exception>
	public FileStream Open(string name, string folder)
	{
		var path = GuardedPath(name, folder);
		if (!File.Exists(path))
		{
			throw new KeyNotFoundException($"Backup {name} does not exist");
		}

		return File.OpenRead(path);
	}

	/// <summary>
	///     Validates a backup archive, stages its contents and replaces the live database.
	///     The caller must restart the application afterwards.
	/// </summary>
	/// <param name="zipStream">The archive stream.</param>
	/// <param name="folder">The configured backup folder, used for the pre-restore Postgres safety dump.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">The archive does not contain the required database file.</exception>
	/// <exception cref="NotSupportedException">Postgres restore requires psql which is not on the PATH.</exception>
	public async Task RestoreAsync(Stream zipStream, string folder, CancellationToken cancellationToken = default)
	{
		var staged = Path.Combine(Path.GetTempPath(), "submarine-restore-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(staged);
		try
		{
			using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
			{
				var hasSqlite = archive.GetEntry("submarine.db") is not null;
				var hasPostgres = archive.GetEntry("submarine.sql") is not null;
				if (!hasSqlite && !hasPostgres)
				{
					throw new InvalidOperationException("Archive does not contain a database backup");
				}

				var stagedRoot = Path.GetFullPath(staged) + Path.DirectorySeparatorChar;
				foreach (var entry in archive.Entries)
				{
					var target = Path.GetFullPath(Path.Combine(staged, entry.FullName));
					if (!target.StartsWith(stagedRoot, StringComparison.Ordinal))
					{
						throw new InvalidOperationException($"Archive entry '{entry.FullName}' escapes the extraction directory");
					}

					if (entry.FullName.EndsWith('/'))
					{
						Directory.CreateDirectory(target);
						continue;
					}

					Directory.CreateDirectory(Path.GetDirectoryName(target)!);
					await entry.ExtractToFileAsync(target, overwrite: true, cancellationToken);
				}
			}

			var provider = SubmarineDatabase.Provider(configuration);
			if (provider == SubmarineDatabase.Postgres)
			{
				await RestorePostgresAsync(staged, folder, cancellationToken);
				return;
			}

			RestoreSqlite(staged);
		}
		finally
		{
			Directory.Delete(staged, recursive: true);
		}
	}

	/// <summary>
	///     The persisted data directory, used for both providers so backups and disk-space
	///     reporting land on the mounted volume rather than the container overlay.
	/// </summary>
	public string AppData() => dataDirectory.Path;

	private async Task AddDatabaseAsync(ZipArchive archive, CancellationToken cancellationToken)
	{
		if (SubmarineDatabase.Provider(configuration) == SubmarineDatabase.Postgres)
		{
			await DumpPostgresAsync(archive, cancellationToken);
			return;
		}

		var entry = archive.CreateEntry("submarine.db", CompressionLevel.Optimal);
		var tempFile = Path.Combine(Path.GetTempPath(), "submarine-backup-" + Guid.NewGuid().ToString("N") + ".db");
		try
		{
			// Online backup API: safe against concurrent writes on a live database.
			await using var source = new SqliteConnection(SubmarineDatabase.SqliteConnectionString(configuration));
			await source.OpenAsync(cancellationToken);
			await using var destination = new SqliteConnection("Data Source=" + tempFile);
			await destination.OpenAsync(cancellationToken);
			source.BackupDatabase(destination);

			await using var sourceStream = File.OpenRead(tempFile);
			await using var entryStream = await entry.OpenAsync(cancellationToken);
			await sourceStream.CopyToAsync(entryStream, cancellationToken);
		}
		finally
		{
			File.Delete(tempFile);
		}
	}

	private async Task DumpPostgresAsync(ZipArchive archive, CancellationToken cancellationToken)
	{
		var executable = FindOnPath("pg_dump");
		if (executable is null)
		{
			logger.LogWarning("pg_dump was not found on the PATH, the backup will not contain the database");
			var readme = archive.CreateEntry("README.txt");
			await using var writer = new StreamWriter(await readme.OpenAsync(cancellationToken));
			await writer.WriteAsync("The Postgres database was not included because pg_dump is not available.");
			return;
		}

		var builder = new NpgsqlConnectionStringBuilder(SubmarineDatabase.PostgresConnectionString(configuration));
		var dumpFile = Path.Combine(Path.GetTempPath(), "submarine-backup-" + Guid.NewGuid().ToString("N") + ".sql");
		try
		{
			await RunPgDumpAsync(executable, builder, dumpFile, cancellationToken);

			var entry = archive.CreateEntry("submarine.sql", CompressionLevel.Optimal);
			await using var sourceStream = File.OpenRead(dumpFile);
			await using var entryStream = await entry.OpenAsync(cancellationToken);
			await sourceStream.CopyToAsync(entryStream, cancellationToken);
		}
		finally
		{
			File.Delete(dumpFile);
		}
	}

	private async Task RunPgDumpAsync(string executable, NpgsqlConnectionStringBuilder builder, string outputFile, CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo(executable)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true
		};
		ApplyConnectionArguments(startInfo, builder);
		startInfo.ArgumentList.Add("-f");
		startInfo.ArgumentList.Add(outputFile);
		if (!string.IsNullOrEmpty(builder.Password))
		{
			startInfo.Environment["PGPASSWORD"] = builder.Password;
		}

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start pg_dump");
		await process.WaitForExitAsync(cancellationToken);
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"pg_dump exited with {process.ExitCode}: {await process.StandardError.ReadToEndAsync(cancellationToken)}");
		}
	}

	private void RestoreSqlite(string stagedDirectory)
	{
		var stagedDb = Path.Combine(stagedDirectory, "submarine.db");
		if (!File.Exists(stagedDb))
		{
			throw new InvalidOperationException("Archive does not contain submarine.db for the Sqlite provider");
		}

		ValidateSqliteFile(stagedDb);

		var builder = new SqliteConnectionStringBuilder(SubmarineDatabase.SqliteConnectionString(configuration));
		var liveDb = Path.Combine(dataDirectory.Path, Path.GetFileName(builder.DataSource));
		var pendingPath = liveDb + ".restore";
		Directory.CreateDirectory(Path.GetDirectoryName(liveDb)!);

		// The live database may still be open (EF pool, command executor, log sink): stage the
		// validated file next to it and swap it in at the next application start instead of
		// overwriting it here, which would corrupt or lose writes on those open connections.
		File.Copy(stagedDb, pendingPath, overwrite: true);
		logger.LogInformation("Staged Sqlite restore to {Path}; it will be applied on the next application start", pendingPath);
	}

	private static void ValidateSqliteFile(string path)
	{
		using (var stream = File.OpenRead(path))
		{
			var header = new byte[16];
			var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
			if (read != header.Length || Encoding.ASCII.GetString(header) != "SQLite format 3\0")
			{
				throw new InvalidOperationException("The archive's database file is not a valid Sqlite database");
			}
		}

		using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA integrity_check;";
		var result = command.ExecuteScalar() as string;
		if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException($"The archive's database failed integrity check: {result}");
		}
	}

	private async Task RestorePostgresAsync(string stagedDirectory, string folder, CancellationToken cancellationToken)
	{
		var stagedSql = Path.Combine(stagedDirectory, "submarine.sql");
		if (!File.Exists(stagedSql))
		{
			throw new InvalidOperationException("Archive does not contain submarine.sql for the Postgres provider");
		}

		var executable = FindOnPath("psql");
		if (executable is null)
		{
			throw new NotSupportedException("Postgres restore requires psql on the PATH");
		}

		var builder = new NpgsqlConnectionStringBuilder(SubmarineDatabase.PostgresConnectionString(configuration));

		// Safety net: dump the live database before touching it, so a bad archive is recoverable.
		await SafetyDumpPostgresAsync(builder, folder, cancellationToken);

		// Combine the schema reset and the restore script into one --single-transaction run, so a
		// failure anywhere in the archive rolls back the DROP too instead of leaving an empty database.
		var combined = Path.Combine(Path.GetTempPath(), "submarine-restore-" + Guid.NewGuid().ToString("N") + ".sql");
		try
		{
			await using (var writer = new StreamWriter(combined))
			{
				await writer.WriteLineAsync("DROP SCHEMA public CASCADE;");
				await writer.WriteLineAsync("CREATE SCHEMA public;");
				await writer.WriteAsync(await File.ReadAllTextAsync(stagedSql, cancellationToken));
			}

			await RunPsqlAsync(executable, builder, ["--single-transaction", "-v", "ON_ERROR_STOP=1", "-q", "-f", combined], cancellationToken);
		}
		finally
		{
			File.Delete(combined);
		}

		logger.LogInformation("Restored Postgres database from backup");
	}

	private async Task SafetyDumpPostgresAsync(NpgsqlConnectionStringBuilder builder, string folder, CancellationToken cancellationToken)
	{
		var executable = FindOnPath("pg_dump");
		if (executable is null)
		{
			logger.LogWarning("pg_dump was not found on the PATH; skipping the pre-restore safety dump");
			return;
		}

		var directory = BackupDirectory(folder);
		Directory.CreateDirectory(directory);
		var safetyPath = Path.Combine(directory, $"pre-restore-safety_{timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}.sql");
		try
		{
			await RunPgDumpAsync(executable, builder, safetyPath, cancellationToken);
			logger.LogInformation("Saved a pre-restore safety dump to {Path}", safetyPath);
		}
		catch (InvalidOperationException ex)
		{
			logger.LogWarning(ex, "Pre-restore safety dump failed, continuing with the restore");
		}
	}

	private static void ApplyConnectionArguments(ProcessStartInfo startInfo, NpgsqlConnectionStringBuilder builder)
	{
		if (!string.IsNullOrEmpty(builder.Host))
		{
			startInfo.ArgumentList.Add("-h");
			startInfo.ArgumentList.Add(builder.Host);
		}

		startInfo.ArgumentList.Add("-p");
		startInfo.ArgumentList.Add(builder.Port.ToString(CultureInfo.InvariantCulture));
		if (!string.IsNullOrEmpty(builder.Username))
		{
			startInfo.ArgumentList.Add("-U");
			startInfo.ArgumentList.Add(builder.Username);
		}

		startInfo.ArgumentList.Add("-d");
		startInfo.ArgumentList.Add(builder.Database ?? "submarine");
	}

	private async Task RunPsqlAsync(string executable, NpgsqlConnectionStringBuilder builder, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo(executable)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true
		};
		ApplyConnectionArguments(startInfo, builder);
		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		if (!string.IsNullOrEmpty(builder.Password))
		{
			startInfo.Environment["PGPASSWORD"] = builder.Password;
		}

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start psql");
		await process.WaitForExitAsync(cancellationToken);
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"psql exited with {process.ExitCode}: {await process.StandardError.ReadToEndAsync(cancellationToken)}");
		}
	}

	private async Task AddFileIfExistsAsync(ZipArchive archive, string entryName, string sourcePath, CancellationToken cancellationToken)
	{
		if (!File.Exists(sourcePath))
		{
			return;
		}

		var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
		await using var sourceStream = File.OpenRead(sourcePath);
		await using var entryStream = await entry.OpenAsync(cancellationToken);
		await sourceStream.CopyToAsync(entryStream, cancellationToken);
	}

	private async Task AddDefinitionsAsync(ZipArchive archive, CancellationToken cancellationToken)
	{
		var definitions = Path.Combine(dataDirectory.Path, "definitions");
		if (!Directory.Exists(definitions))
		{
			return;
		}

		foreach (var file in Directory.EnumerateFiles(definitions, "*.yml", SearchOption.TopDirectoryOnly))
		{
			await AddFileIfExistsAsync(archive, "definitions/" + Path.GetFileName(file), file, cancellationToken);
		}
	}

	private void ApplyRetention(string folder, int retention)
	{
		var directory = BackupDirectory(folder);
		var valid = List(folder);
		foreach (var entry in valid.Skip(Math.Max(retention, 0)))
		{
			try
			{
				File.Delete(Path.Combine(directory, entry.Name));
			}
			catch (IOException ex)
			{
				logger.LogWarning(ex, "Failed to delete expired backup {Name}", entry.Name);
			}
		}
	}

	private string GuardedPath(string name, string folder)
	{
		if (!FileNamePattern().IsMatch(name))
		{
			throw new InvalidOperationException("Invalid backup name");
		}

		return Path.Combine(BackupDirectory(folder), name);
	}

	private string BackupDirectory(string folder)
	{
		return string.IsNullOrEmpty(folder)
			? Path.Combine(AppData(), "backups")
			: Path.GetFullPath(folder, AppData());
	}

	private static string? FindOnPath(string fileName)
		=> Environment.GetEnvironmentVariable("PATH")?
			.Split(Path.PathSeparator)
			.FirstOrDefault(directory => File.Exists(Path.Combine(directory, fileName))) is { } directory
			? Path.Combine(directory, fileName)
			: null;
}
