using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Response;

namespace Submarine.Api.Services;

/// <summary>
///     Creates and manages zip backups of the database and configuration. There is no restore endpoint: to
///     restore, stop Submarine, replace the database file with the one contained in a backup, then start again.
/// </summary>
public class BackupService
{
	private const string FilePrefix = "submarine_backup_";
	private const string FileExtension = ".zip";
	private const string DateFormat = "yyyyMMddHHmmss";

	private static readonly Regex FileNamePattern = new(@"^submarine_backup_\d{14}\.zip$", RegexOptions.Compiled);

	private readonly IConfiguration _configuration;
	private readonly ILogger<BackupService> _logger;

	public BackupService(IConfiguration configuration, ILogger<BackupService> logger)
	{
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	///     Creates a backup zip and applies retention, deleting the oldest backups beyond the configured count
	/// </summary>
	public async Task<BackupEntryResponse> CreateAsync(CancellationToken cancellationToken = default)
	{
		var directory = GetBackupDirectory();
		Directory.CreateDirectory(directory);

		var name = $"{FilePrefix}{DateTimeOffset.UtcNow.ToString(DateFormat)}{FileExtension}";
		var path = Path.Combine(directory, name);

		using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
		{
			await AddDatabaseAsync(archive, cancellationToken);
			AddAppSettings(archive);
		}

		ApplyRetention(directory);

		var info = new FileInfo(path);

		return new BackupEntryResponse(name, info.Length, info.CreationTimeUtc);
	}

	/// <summary>
	///     Lists backups on disk, newest first
	/// </summary>
	public IReadOnlyList<BackupEntryResponse> List()
	{
		var directory = GetBackupDirectory();

		if (!Directory.Exists(directory))
			return Array.Empty<BackupEntryResponse>();

		return EnumerateBackupFiles(directory)
			.OrderByDescending(f => f.CreationTimeUtc)
			.Select(f => new BackupEntryResponse(f.Name, f.Length, f.CreationTimeUtc))
			.ToList();
	}

	/// <summary>
	///     Deletes a backup by file name
	/// </summary>
	/// <exception cref="BadRequestException">the name does not match the backup file name pattern</exception>
	/// <exception cref="NotFoundException">no backup with that name exists</exception>
	public void Delete(string name)
		=> File.Delete(ResolvePath(name));

	/// <summary>
	///     Resolves a backup file name to its full path on disk, guarding against path traversal by only accepting
	///     names matching the generated backup file pattern
	/// </summary>
	/// <exception cref="BadRequestException">the name does not match the backup file name pattern</exception>
	/// <exception cref="NotFoundException">no backup with that name exists</exception>
	public string ResolvePath(string name)
	{
		if (!FileNamePattern.IsMatch(name))
			throw new BadRequestException($"Invalid backup file name '{name}'");

		var path = Path.Combine(GetBackupDirectory(), name);

		if (!File.Exists(path))
			throw new NotFoundException();

		return path;
	}

	private void ApplyRetention(string directory)
	{
		var retention = _configuration.GetValue<int?>("Backup:Retention") ?? 7;

		var stale = EnumerateBackupFiles(directory)
			.OrderByDescending(f => f.CreationTimeUtc)
			.Skip(retention);

		foreach (var file in stale)
			try
			{
				file.Delete();
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Deleting stale backup {File} failed", file.Name);
			}
	}

	private static IEnumerable<FileInfo> EnumerateBackupFiles(string directory)
		=> Directory.EnumerateFiles(directory, $"{FilePrefix}*{FileExtension}")
			.Where(f => FileNamePattern.IsMatch(Path.GetFileName(f)))
			.Select(f => new FileInfo(f));

	private string GetBackupDirectory()
		=> Path.GetFullPath(_configuration.GetValue<string>("Backup:Path") ?? "backups");

	private async Task AddDatabaseAsync(ZipArchive archive, CancellationToken cancellationToken)
	{
		var provider = _configuration.GetValue<string>("Database:Provider") ?? "Sqlite";

		if (!string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
		{
			var entry = archive.CreateEntry("README.txt");
			await using var writer = new StreamWriter(entry.Open());
			await writer.WriteAsync(
				$"Database backup skipped: provider '{provider}' is not file-based. This is a config-only backup.");
			return;
		}

		var connectionString = _configuration.GetConnectionString("SqliteConnection");

		if (string.IsNullOrEmpty(connectionString))
		{
			_logger.LogWarning("No SqliteConnection connection string configured, skipping database backup");
			return;
		}

		var dbPath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);

		if (!File.Exists(dbPath))
		{
			_logger.LogWarning("Sqlite database file {Path} not found, skipping database backup", dbPath);
			return;
		}

		// Never zip the live database file directly: back it up via the Sqlite backup API into a temp copy first,
		// which is safe to read while the live database is open for writes.
		var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

		try
		{
			await using (var source = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
			// Pooling=false: a pooled connection keeps a native handle on the temp file open past Dispose,
			// which would make the File.Delete below fail with a sharing violation on Windows.
			await using (var destination = new SqliteConnection($"Data Source={tempPath};Pooling=false"))
			{
				await source.OpenAsync(cancellationToken);
				await destination.OpenAsync(cancellationToken);

				source.BackupDatabase(destination);
			}

			archive.CreateEntryFromFile(tempPath, Path.GetFileName(dbPath));
		}
		finally
		{
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
	}

	private static void AddAppSettings(ZipArchive archive)
	{
		var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

		if (File.Exists(path))
			archive.CreateEntryFromFile(path, "appsettings.json");
	}
}
