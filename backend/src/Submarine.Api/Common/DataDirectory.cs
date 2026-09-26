namespace Submarine.Api.Common;

/// <summary>
///     Absolute path to the persisted data directory (holds the database, log files and
///     DataProtection keys), so they survive container restarts under a mounted volume.
/// </summary>
public sealed record DataDirectory(string Path);
