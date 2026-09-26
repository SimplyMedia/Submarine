using Submarine.Core.Languages;
using Submarine.Core.MediaFiles;
using Submarine.Core.Quality;

namespace Submarine.Infrastructure.Import;

/// <summary>
///     How a file is placed at its destination during import.
/// </summary>
public enum ImportMode
{
	/// <summary>Hardlink or move the file from its source location.</summary>
	MOVE,

	/// <summary>Copy the file, keeping the source.</summary>
	COPY,

	/// <summary>The file already sits at its destination; only the database row is created.</summary>
	ADOPT
}

/// <summary>
///     Outcome of importing one candidate file.
/// </summary>
/// <param name="SourcePath">Path of the candidate file.</param>
/// <param name="Imported">Whether the file was imported.</param>
/// <param name="DestinationPath">Final path, when imported.</param>
/// <param name="IsUpgrade">Whether an existing file was replaced.</param>
/// <param name="Rejection">Rejection reason, when not imported.</param>
/// <param name="Message">Human readable outcome message.</param>
public sealed record ImportFileOutcome(
	string SourcePath,
	bool Imported,
	string? DestinationPath,
	bool IsUpgrade,
	ImportRejectionReason? Rejection,
	string? Message);

/// <summary>
///     Summary of one import run, covering every candidate file that was considered.
/// </summary>
/// <param name="Files">Outcome of every candidate file.</param>
public sealed record ImportRunSummary(IReadOnlyList<ImportFileOutcome> Files)
{
	/// <summary>Whether at least one file was imported.</summary>
	public bool AnyImported => Files.Any(x => x.Imported);

	/// <summary>Whether every candidate file was rejected.</summary>
	public bool AllRejected => Files.Count > 0 && Files.All(x => !x.Imported);
}

/// <summary>
///     A user's explicit choice for a manual import file.
/// </summary>
/// <param name="Path">Absolute path of the file.</param>
/// <param name="SeriesId">Target series, when importing an episode file.</param>
/// <param name="EpisodeIds">Target episodes, when importing an episode file.</param>
/// <param name="MovieId">Target movie, when importing a movie file.</param>
/// <param name="MediaVersionId">Target version.</param>
/// <param name="Quality">Quality override, falls back to the parsed quality.</param>
/// <param name="Languages">Language override, falls back to the parsed languages.</param>
/// <param name="ReleaseGroup">Release group override.</param>
/// <param name="DownloadId">Originating tracked download id, if any, so it can be marked imported.</param>
public sealed record ManualImportSelection(
	string Path,
	int? SeriesId,
	IReadOnlyList<int>? EpisodeIds,
	int? MovieId,
	int MediaVersionId,
	QualityModel? Quality,
	IReadOnlyList<Language>? Languages,
	string? ReleaseGroup,
	string? DownloadId);

/// <summary>
///     A candidate file found during manual import analysis, with the pipeline's best guess at its match.
/// </summary>
/// <param name="Path">Absolute path of the file.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="SeriesId">Guessed series, if any.</param>
/// <param name="EpisodeIds">Guessed episodes, if any.</param>
/// <param name="MovieId">Guessed movie, if any.</param>
/// <param name="MediaVersionId">Guessed version, if any.</param>
/// <param name="Quality">Parsed quality.</param>
/// <param name="Languages">Parsed languages.</param>
/// <param name="ReleaseGroup">Parsed release group.</param>
/// <param name="Rejection">Why the automated pipeline would reject this file, if it would.</param>
public sealed record ManualImportCandidate(
	string Path,
	long Size,
	int? SeriesId,
	IReadOnlyList<int> EpisodeIds,
	int? MovieId,
	int? MediaVersionId,
	QualityModel Quality,
	IReadOnlyList<Language> Languages,
	string? ReleaseGroup,
	ImportRejectionReason? Rejection);
