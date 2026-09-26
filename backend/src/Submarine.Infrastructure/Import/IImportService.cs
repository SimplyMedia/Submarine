namespace Submarine.Infrastructure.Import;

/// <summary>
///     Imports finished downloads and manually selected files into the library.
/// </summary>
public interface IImportService
{
	/// <summary>
	///     Import a tracked download's output. Matches files to the download's known episodes or movie, applies
	///     the quality profile's upgrade rules, and moves or hardlinks accepted files into place.
	/// </summary>
	Task<ImportRunSummary> ImportTrackedDownloadAsync(int trackedDownloadId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Import files with explicit user choices, bypassing automated matching and quality gating.
	/// </summary>
	Task<ImportRunSummary> ImportManualAsync(IReadOnlyList<ManualImportSelection> selections, ImportMode mode, CancellationToken cancellationToken = default);

	/// <summary>
	///     Analyze a folder or single file for manual import: parses and matches each candidate without importing it.
	/// </summary>
	Task<IReadOnlyList<ManualImportCandidate>> AnalyzeAsync(
		string path,
		int? seriesId,
		int? movieId,
		string? downloadId,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Re-import a series' version folders in place: registers untracked media files without moving them, and
	///     removes rows for files that vanished from disk.
	/// </summary>
	Task<ImportRunSummary> RescanSeriesAsync(int seriesId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Re-import a movie's version folders in place: registers untracked media files without moving them, and
	///     removes rows for files that vanished from disk.
	/// </summary>
	Task<ImportRunSummary> RescanMovieAsync(int movieId, CancellationToken cancellationToken = default);
}
