namespace Submarine.Api.Models.Response;

/// <summary>
///     Result of a manual import request
/// </summary>
/// <param name="Results">per-file outcome</param>
public record ManualImportResponse(IReadOnlyList<ManualImportFileResult> Results);

/// <summary>
///     Outcome of importing a single manually chosen file
/// </summary>
/// <param name="Path">path of the source file</param>
/// <param name="Imported">whether the file was imported</param>
/// <param name="Error">failure reason, if not imported</param>
public record ManualImportFileResult(string Path, bool Imported, string? Error);
