namespace Submarine.Api.Events;

/// <summary>
///     Raised when a completed download was imported into the library
/// </summary>
/// <param name="SeriesId">Id of the series the import belongs to, if any</param>
/// <param name="MovieId">Id of the movie the import belongs to, if any</param>
/// <param name="Path">library path of the media</param>
/// <param name="Title">title of the imported download</param>
public record MediaImportedEvent(int? SeriesId, int? MovieId, string Path, string Title);
