namespace Submarine.Api.Events;

/// <summary>
///     Raised when a media file in the library was renamed
/// </summary>
/// <param name="SeriesId">Id of the series the file belongs to, if any</param>
/// <param name="MovieId">Id of the movie the file belongs to, if any</param>
/// <param name="Path">library path of the media</param>
/// <param name="Title">title of the renamed media</param>
public record MediaRenamedEvent(int? SeriesId, int? MovieId, string Path, string Title);
