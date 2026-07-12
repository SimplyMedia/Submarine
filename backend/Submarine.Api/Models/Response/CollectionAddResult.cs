namespace Submarine.Api.Models.Response;

/// <summary>
///     Result of adding the missing movies of a collection to the library
/// </summary>
public record CollectionAddResult(IReadOnlyList<CollectionAddedMovie> Added, IReadOnlyList<CollectionAddFailure> Failed);

/// <summary>
///     A movie that was successfully added as part of a collection add
/// </summary>
public record CollectionAddedMovie(int Id, int TmdbId, string Title);

/// <summary>
///     A movie that failed to be added as part of a collection add
/// </summary>
public record CollectionAddFailure(int TmdbId, string Title, string Reason);
