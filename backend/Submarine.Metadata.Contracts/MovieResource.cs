namespace Submarine.Metadata.Contracts;

/// <summary>
///     A normalized movie, independent of the metadata source it was resolved from
/// </summary>
/// <param name="TmdbId">TheMovieDB identifier of this movie</param>
/// <param name="ImdbId">IMDb identifier of this movie, if known</param>
/// <param name="Title">title of this movie</param>
/// <param name="SortTitle">title used for sorting purposes</param>
/// <param name="Overview">synopsis of this movie</param>
/// <param name="ReleaseDate">release date of this movie</param>
/// <param name="Year">release year of this movie</param>
/// <param name="Runtime">runtime of this movie in minutes</param>
/// <param name="Genres">genres of this movie</param>
/// <param name="Studio">studio that produced this movie</param>
/// <param name="ImageUrl">url of the poster image of this movie</param>
/// <param name="Editions">known editions of this movie</param>
/// <param name="TmdbCollectionId">TheMovieDB identifier of the collection this movie belongs to, if any</param>
/// <param name="CollectionTitle">title of the collection this movie belongs to, if any</param>
public record MovieResource(
	int TmdbId,
	string? ImdbId,
	string Title,
	string? SortTitle,
	string? Overview,
	DateOnly? ReleaseDate,
	int? Year,
	int? Runtime,
	IReadOnlyList<string> Genres,
	string? Studio,
	string? ImageUrl,
	IReadOnlyList<string> Editions,
	int? TmdbCollectionId = null,
	string? CollectionTitle = null);
