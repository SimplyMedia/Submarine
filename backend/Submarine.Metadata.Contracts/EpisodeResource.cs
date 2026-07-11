namespace Submarine.Metadata.Contracts;

/// <summary>
///     A single episode of a <see cref="SeriesResource" />
/// </summary>
/// <param name="TvdbId">TheTVDB identifier of this episode, if known</param>
/// <param name="TmdbId">TheMovieDB identifier of this episode, if known</param>
/// <param name="Title">title of this episode</param>
/// <param name="Overview">synopsis of this episode</param>
/// <param name="AirDate">original air date of this episode</param>
/// <param name="Runtime">runtime of this episode in minutes</param>
/// <param name="Numbers">numbering of this episode across the supported orderings</param>
public record EpisodeResource(
	int? TvdbId,
	int? TmdbId,
	string? Title,
	string? Overview,
	DateOnly? AirDate,
	int? Runtime,
	IReadOnlyList<EpisodeNumber> Numbers);
