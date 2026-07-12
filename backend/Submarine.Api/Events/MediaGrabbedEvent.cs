namespace Submarine.Api.Events;

/// <summary>
///     Raised when a release for media was grabbed on a download client
/// </summary>
/// <param name="SeriesId">Id of the series the release belongs to, if any</param>
/// <param name="MovieId">Id of the movie the release belongs to, if any</param>
/// <param name="Path">library path of the media</param>
/// <param name="Title">title of the grabbed release</param>
public record MediaGrabbedEvent(int? SeriesId, int? MovieId, string Path, string Title);
