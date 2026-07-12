namespace Submarine.Api.Models.Response;

/// <summary>
///     A proposed import for a single library folder
/// </summary>
/// <param name="Folder">the folder this proposal is for</param>
/// <param name="SuggestedTvdbId">TheTVDB id of the best matching series, if any</param>
/// <param name="SuggestedTmdbId">TheMovieDB id of the best matching movie, if any</param>
/// <param name="SuggestedTitle">title of the best match, if any</param>
/// <param name="Year">year parsed from the folder name, if any</param>
/// <param name="Alternatives">other matching candidates</param>
/// <param name="Files">video files found in the folder</param>
public record LibraryImportProposal(
	string Folder,
	int? SuggestedTvdbId,
	int? SuggestedTmdbId,
	string? SuggestedTitle,
	int? Year,
	IReadOnlyList<LibraryImportAlternative> Alternatives,
	IReadOnlyList<LibraryImportProposalFile> Files);

/// <summary>
///     An alternative match for a library folder
/// </summary>
/// <param name="Id">metadata id of the candidate (TVDB for series, TMDB for movies)</param>
/// <param name="Title">title of the candidate</param>
public record LibraryImportAlternative(int Id, string Title);

/// <summary>
///     A video file found while scanning a library folder, with its parsed guesses
/// </summary>
/// <param name="Path">path of the file</param>
/// <param name="Season">parsed season number, if any</param>
/// <param name="Episodes">parsed episode numbers</param>
/// <param name="Absolute">parsed absolute episode numbers</param>
/// <param name="QualityName">parsed quality name</param>
public record LibraryImportProposalFile(
	string Path,
	int? Season,
	IReadOnlyList<int> Episodes,
	IReadOnlyList<int> Absolute,
	string QualityName);
