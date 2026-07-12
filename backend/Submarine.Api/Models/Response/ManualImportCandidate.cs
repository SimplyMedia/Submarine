using Submarine.Core.Languages;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A candidate file for manual import, with its parsed guesses and inferred media
/// </summary>
/// <param name="Path">path of the file</param>
/// <param name="Size">size of the file in bytes</param>
/// <param name="ParsedTitle">title parsed from the file name, if any</param>
/// <param name="Season">parsed season number, if any</param>
/// <param name="Episodes">parsed episode numbers</param>
/// <param name="SuggestedSeriesId">inferred Series id, if any</param>
/// <param name="SuggestedMovieId">inferred Movie id, if any</param>
/// <param name="SuggestedEpisodeIds">inferred Episode ids, if any</param>
/// <param name="QualityName">parsed quality name</param>
/// <param name="Languages">parsed languages</param>
/// <param name="ReleaseGroup">parsed release group, if any</param>
public record ManualImportCandidate(
	string Path,
	long Size,
	string? ParsedTitle,
	int? Season,
	IReadOnlyList<int> Episodes,
	int? SuggestedSeriesId,
	int? SuggestedMovieId,
	IReadOnlyList<int> SuggestedEpisodeIds,
	string QualityName,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup);
