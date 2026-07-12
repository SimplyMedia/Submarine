namespace Submarine.Metadata.Contracts;

/// <summary>
///     A normalized movie collection, independent of the metadata source it was resolved from
/// </summary>
/// <param name="TmdbCollectionId">TheMovieDB identifier of this collection</param>
/// <param name="Title">title of this collection</param>
/// <param name="Overview">synopsis of this collection</param>
/// <param name="Movies">movies belonging to this collection</param>
public record CollectionResource(
	int TmdbCollectionId,
	string Title,
	string? Overview,
	IReadOnlyList<MovieResource> Movies);
