namespace Submarine.Api.Events;

/// <summary>
///     Raised when a series or movie was deleted from the library
/// </summary>
/// <param name="SeriesId">Id of the deleted series, if any</param>
/// <param name="MovieId">Id of the deleted movie, if any</param>
/// <param name="Title">title of the deleted media</param>
/// <param name="Path">library path of the media, if known</param>
public record MediaDeletedEvent(int? SeriesId, int? MovieId, string Title, string? Path);
