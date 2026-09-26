namespace Submarine.Contracts.Metadata;

public record AlternateTitleResource(string Title, string? Language);

public record SeriesResource(
	int TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	string? SortTitle,
	string? Overview,
	DateOnly? FirstAired,
	SeriesStatus Status,
	int? Runtime,
	string? Network,
	IReadOnlyList<string> Genres,
	string? Certification,
	IReadOnlyList<SeasonResource> Seasons,
	IReadOnlyList<EpisodeResource> Episodes,
	string? PosterUrl,
	string? BackdropUrl,
	int? Year,
	IReadOnlyList<AlternateTitleResource> AlternateTitles);

public record SeasonResource(
	int SeasonNumber,
	string? Name,
	int EpisodeCount);

public record EpisodeResource(
	int? TvdbId,
	int? TmdbId,
	string? Title,
	string? Overview,
	DateOnly? AirDate,
	DateOnly? AirDateUtc,
	int? Runtime,
	IReadOnlyList<EpisodeNumber> Numbers,
	string? ImageUrl);

public record EpisodeNumber(
	EpisodeOrdering Ordering,
	int? SeasonNumber,
	int? Number,
	int? AbsoluteNumber);
