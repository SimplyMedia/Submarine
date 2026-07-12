namespace Submarine.Api.Models.Response;

/// <summary>
///     A movie collection as grouped from movies already in the library
/// </summary>
public record CollectionListItem(
	int TmdbCollectionId,
	string Title,
	int MovieCount,
	IReadOnlyList<CollectionListMovie> Movies);

/// <summary>
///     A library movie belonging to a <see cref="CollectionListItem" />
/// </summary>
public record CollectionListMovie(int Id, string Title, int? Year, bool Monitored, bool HasFile);
