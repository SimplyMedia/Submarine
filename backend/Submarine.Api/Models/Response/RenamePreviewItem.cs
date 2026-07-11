namespace Submarine.Api.Models.Response;

/// <summary>
///     The current and re-rendered relative path of an episode file
/// </summary>
/// <param name="EpisodeFileId">Id of the episode file</param>
/// <param name="CurrentPath">Current path of the file, relative to the series path</param>
/// <param name="NewPath">Path the file would be renamed to, relative to the series path</param>
public record RenamePreviewItem(int EpisodeFileId, string CurrentPath, string NewPath);
