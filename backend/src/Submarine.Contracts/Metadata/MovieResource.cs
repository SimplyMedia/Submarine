namespace Submarine.Contracts.Metadata;

public record MovieResource(
	int TmdbId,
	string? ImdbId,
	string Title,
	string? OriginalTitle,
	string? SortTitle,
	string? Overview,
	DateOnly? InCinemasDate,
	DateOnly? DigitalReleaseDate,
	DateOnly? PhysicalReleaseDate,
	MovieStatus Status,
	int? Year,
	int? Runtime,
	IReadOnlyList<string> Genres,
	string? Studio,
	string? Certification,
	string? PosterUrl,
	string? BackdropUrl,
	string? YouTubeTrailerId,
	int? TmdbCollectionId,
	string? CollectionTitle,
	IReadOnlyList<AlternateTitleResource> AlternateTitles,
	string? OriginalLanguage = null,
	IReadOnlyList<string>? Keywords = null,
	IReadOnlyList<MovieCreditResource>? Credits = null);

public sealed record MovieCreditResource(int TmdbPersonId, string Name, string? ProfileUrl, string? Department, string? Job, string? Character, int? Order);

public record CollectionResource(
	int TmdbCollectionId,
	string Title,
	string? Overview,
	string? PosterUrl,
	IReadOnlyList<MovieResource> Movies);
