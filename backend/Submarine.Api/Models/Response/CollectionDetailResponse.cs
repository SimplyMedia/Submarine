namespace Submarine.Api.Models.Response;

/// <summary>
///     A movie collection combining library movies with missing entries known from the metadata proxy
/// </summary>
public record CollectionDetailResponse(
	int TmdbCollectionId,
	string Title,
	string? Overview,
	IReadOnlyList<CollectionEntry> Movies);

/// <summary>
///     A single movie in a <see cref="CollectionDetailResponse" />, either already in the library or missing
/// </summary>
public record CollectionEntry(
	bool InLibrary,
	int? Id,
	int TmdbId,
	string Title,
	int? Year,
	bool? Monitored,
	bool? HasFile);
