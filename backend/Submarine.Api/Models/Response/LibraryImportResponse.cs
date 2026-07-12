namespace Submarine.Api.Models.Response;

/// <summary>
///     Result of adopting a batch of library folders
/// </summary>
/// <param name="Results">per-folder outcome</param>
public record LibraryImportResponse(IReadOnlyList<LibraryImportItemResult> Results);

/// <summary>
///     Outcome of adopting a single library folder
/// </summary>
/// <param name="Folder">the folder this result is for</param>
/// <param name="Success">whether the folder was added</param>
/// <param name="MediaId">id of the created Series or Movie, if successful</param>
/// <param name="Error">failure reason, if not successful</param>
public record LibraryImportItemResult(string Folder, bool Success, int? MediaId, string? Error);
