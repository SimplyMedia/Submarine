namespace Submarine.Contracts.Metadata;

/// <summary>
///     A single hit from a series or movie search. Provider names the upstream
///     the hit came from ("tvdb" or "tmdb"); id fields not known for that
///     upstream are null.
/// </summary>
public record SearchResultResource(
	int? TvdbId,
	int? TmdbId,
	string? ImdbId,
	string Title,
	int? Year,
	string? Overview,
	string? PosterUrl,
	string? Status,
	string Provider);
