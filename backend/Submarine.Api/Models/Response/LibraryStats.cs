namespace Submarine.Api.Models.Response;

/// <summary>
///     Aggregate counts describing the current state of the library
/// </summary>
public record LibraryStats(
	int SeriesCount,
	int EndedSeriesCount,
	int ContinuingSeriesCount,
	int MovieCount,
	int EpisodeCount,
	int EpisodeFileCount,
	int MovieFileCount,
	long TotalFileSize,
	IReadOnlyList<RootFolderStats> PerRootFolder,
	HistoryCounts HistoryCounts);

/// <summary>
///     Disk usage of a single root folder
/// </summary>
public record RootFolderStats(string Path, long? FreeSpace, long? TotalCapacity);

/// <summary>
///     History event counts over the last 30 days
/// </summary>
public record HistoryCounts(int Grabbed, int Imported, int Failed);
