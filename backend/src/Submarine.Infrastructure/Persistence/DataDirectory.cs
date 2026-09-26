namespace Submarine.Infrastructure.Persistence;

/// <summary>
///     Absolute path to the persisted data directory (holds the database, log files, backups,
///     synced indexer definitions and DataProtection keys), so they survive container restarts
///     under a mounted volume (`/config` in the Docker image).
/// </summary>
public sealed record DataDirectory(string Path);
