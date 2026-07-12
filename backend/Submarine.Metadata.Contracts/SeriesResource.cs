namespace Submarine.Metadata.Contracts;

/// <summary>
///     A normalized series, independent of the metadata source it was resolved from
/// </summary>
/// <param name="TvdbId">TheTVDB identifier of this series</param>
/// <param name="TmdbId">TheMovieDB identifier of this series, if known</param>
/// <param name="Title">title of this series</param>
/// <param name="SortTitle">title used for sorting purposes</param>
/// <param name="Overview">synopsis of this series</param>
/// <param name="FirstAired">date this series first aired</param>
/// <param name="Status">broadcast status of this series</param>
/// <param name="Runtime">average runtime of an episode in minutes</param>
/// <param name="Network">network this series airs on</param>
/// <param name="Genres">genres of this series</param>
/// <param name="Seasons">seasons of this series</param>
/// <param name="Episodes">episodes of this series</param>
/// <param name="ImageUrl">url of the poster image of this series</param>
/// <param name="Year">year this series first aired</param>
/// <param name="BackdropUrl">url of the backdrop image of this series, if known</param>
public record SeriesResource(
	int TvdbId,
	int? TmdbId,
	string Title,
	string? SortTitle,
	string? Overview,
	DateOnly? FirstAired,
	SeriesStatus Status,
	int? Runtime,
	string? Network,
	IReadOnlyList<string> Genres,
	IReadOnlyList<SeasonResource> Seasons,
	IReadOnlyList<EpisodeResource> Episodes,
	string? ImageUrl,
	int? Year,
	string? BackdropUrl = null);
