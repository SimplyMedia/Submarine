namespace Submarine.Api.Events;

/// <summary>
///     Raised when a metadata refresh changes the title of an episode that already has a file on disk
/// </summary>
/// <param name="EpisodeId">Id of the episode whose title changed</param>
/// <param name="SeriesId">Id of the series the episode belongs to</param>
/// <param name="OldTitle">The previous title, if any</param>
/// <param name="NewTitle">The new title</param>
public record EpisodeTitleChangedEvent(int EpisodeId, int SeriesId, string? OldTitle, string NewTitle);
