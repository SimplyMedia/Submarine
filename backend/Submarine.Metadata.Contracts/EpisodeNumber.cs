namespace Submarine.Metadata.Contracts;

/// <summary>
///     Numbering of an episode under a given <see cref="EpisodeOrdering" />
/// </summary>
/// <param name="Ordering">ordering scheme this numbering belongs to</param>
/// <param name="SeasonNumber">season number, if applicable for this ordering</param>
/// <param name="Number">episode number within the season, if applicable for this ordering</param>
/// <param name="AbsoluteNumber">absolute episode number, if applicable for this ordering</param>
public record EpisodeNumber(
	EpisodeOrdering Ordering,
	int? SeasonNumber,
	int? Number,
	int? AbsoluteNumber);
