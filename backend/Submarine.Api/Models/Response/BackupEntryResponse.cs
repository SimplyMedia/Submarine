namespace Submarine.Api.Models.Response;

/// <summary>
///     A backup zip file on disk
/// </summary>
public record BackupEntryResponse(string Name, long Size, DateTimeOffset CreatedAt);
