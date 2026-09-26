using Submarine.Core.Entities;

namespace Submarine.Core.Library;

/// <summary>
///     Applies an <see cref="AddMonitorOption" /> to the seasons and episodes of a series.
/// </summary>
public static class MonitorRules
{
	/// <summary>
	///     Set season and episode monitored flags according to the option.
	/// </summary>
	/// <param name="series">Series with seasons and episodes populated.</param>
	/// <param name="option">Which episodes end up monitored.</param>
	/// <param name="monitorSpecials">Whether season 0 takes part at all.</param>
	/// <param name="now">Current UTC time, used by the FUTURE and EXISTING options.</param>
	public static void Apply(Series series, AddMonitorOption option, bool monitorSpecials, DateTime now)
	{
		if (option == AddMonitorOption.SKIP)
		{
			return;
		}

		foreach (var season in series.Seasons)
		{
			if (season.SeasonNumber == 0)
			{
				season.Monitored = monitorSpecials;
			}
			else
			{
				season.Monitored = SeasonMonitored(option, series, season.SeasonNumber);
			}
		}

		foreach (var episode in series.Episodes)
		{
			episode.Monitored = EpisodeMonitored(option, series, episode, monitorSpecials, now);
		}
	}

	private static bool SeasonMonitored(AddMonitorOption option, Series series, int seasonNumber)
		=> option switch
		{
			AddMonitorOption.ALL => true,
			AddMonitorOption.FIRST_SEASON => seasonNumber == FirstRealSeason(series),
			AddMonitorOption.LATEST_SEASON => seasonNumber == LatestRealSeason(series),
			AddMonitorOption.PILOT => seasonNumber == FirstRealSeason(series),
			AddMonitorOption.NONE => false,
			_ => true
		};

	private static bool EpisodeMonitored(AddMonitorOption option, Series series, Episode episode, bool monitorSpecials, DateTime now)
	{
		if (episode.SeasonNumber == 0)
		{
			return monitorSpecials && option is not (AddMonitorOption.PILOT or AddMonitorOption.NONE);
		}

		var aired = episode.AirDateUtc is not null && episode.AirDateUtc <= now;
		return option switch
		{
			AddMonitorOption.ALL => true,
			AddMonitorOption.FUTURE => !aired,
			AddMonitorOption.MISSING => !aired,
			AddMonitorOption.EXISTING => aired,
			AddMonitorOption.PILOT => episode.SeasonNumber == 1 && episode.EpisodeNumber == 1,
			AddMonitorOption.FIRST_SEASON => episode.SeasonNumber == FirstRealSeason(series),
			AddMonitorOption.LATEST_SEASON => episode.SeasonNumber == LatestRealSeason(series),
			AddMonitorOption.NONE => false,
			AddMonitorOption.RECENT => !aired || episode.AirDateUtc >= now.AddDays(-90),
			_ => true
		};
	}

	private static int FirstRealSeason(Series series)
		=> series.Seasons.Where(x => x.SeasonNumber > 0).OrderBy(x => x.SeasonNumber).Select(x => x.SeasonNumber)
			.FirstOrDefault();

	private static int LatestRealSeason(Series series)
	{
		var last = series.Episodes.Where(x => x.SeasonNumber > 0).OrderBy(x => x.SeasonNumber)
			.Select(x => (int?)x.SeasonNumber).LastOrDefault();
		return last ?? FirstRealSeason(series);
	}
}
