using Submarine.Core.Entities;

namespace Submarine.Core.Library;

/// <summary>
///     Statistics for one season of a series.
/// </summary>
/// <param name="EpisodeCount">Monitored episodes in the season.</param>
/// <param name="EpisodeFileCount">Monitored episodes that have a file.</param>
/// <param name="TotalEpisodeCount">All episodes in the season.</param>
/// <param name="SizeOnDisk">Total size of the files, in bytes.</param>
/// <param name="PercentOfEpisodes">Percentage of monitored episodes that have a file.</param>
public sealed record SeasonStatistics(
	int EpisodeCount,
	int EpisodeFileCount,
	int TotalEpisodeCount,
	long SizeOnDisk,
	double PercentOfEpisodes);

/// <summary>
///     Computes per season statistics from episodes and their files.
/// </summary>
public static class SeasonStatisticsCalculator
{
	/// <summary>
	///     Compute statistics for the episodes of a single season.
	/// </summary>
	public static SeasonStatistics Compute(IEnumerable<Episode> episodes)
	{
		var list = episodes.ToList();
		var monitored = list.Where(x => x.Monitored).ToList();
		var withFile = monitored.Count(x => x.Files.Count > 0);
		var size = monitored
			.SelectMany(x => x.Files)
			.Select(x => x.Size)
			.DefaultIfEmpty(0)
			.Sum();
		var percent = monitored.Count == 0
			? 100.0
			: Math.Round(100.0 * withFile / monitored.Count, 1);
		return new SeasonStatistics(monitored.Count, withFile, list.Count, size, percent);
	}
}
