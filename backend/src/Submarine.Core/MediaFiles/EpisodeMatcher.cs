using System.Text.RegularExpressions;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Release;

namespace Submarine.Core.MediaFiles;

/// <summary>
///     Matches a parsed release's season and episode numbers to a series' episodes, honouring the series'
///     numbering scheme (aired, scene or absolute) and, for daily series, the air date embedded in the name.
/// </summary>
public static partial class EpisodeMatcher
{
	/// <summary>
	///     Match episodes for a standard or anime series from parsed season/episode/absolute numbers.
	/// </summary>
	public static IReadOnlyList<Episode> Match(IReadOnlyCollection<Episode> episodes, Series series, SeriesReleaseData? data)
	{
		if (data is null)
		{
			return [];
		}

		if (series.Numbering == SeriesNumbering.ABSOLUTE && data.AbsoluteEpisodes.Count > 0)
		{
			return [.. episodes.Where(episode => data.AbsoluteEpisodes.Contains(episode.AbsoluteEpisodeNumber ?? int.MinValue))];
		}

		if (data.Seasons.Count == 0 && data.AbsoluteEpisodes.Count > 0)
		{
			// anime release with no season number, always match on the absolute number
			return [.. episodes.Where(episode => data.AbsoluteEpisodes.Contains(episode.AbsoluteEpisodeNumber ?? int.MinValue))];
		}

		if (data.Seasons.Count == 1 && data.Episodes.Count > 0)
		{
			var season = data.Seasons[0];
			return [.. episodes.Where(episode => MatchesSeasonEpisode(episode, season, data.Episodes, series.Numbering))];
		}

		if (data.Seasons.Count > 0 && data.Episodes.Count == 0)
		{
			// full or multi season pack, matched per-file elsewhere; nothing to match on season alone
			return [.. episodes.Where(episode => data.Seasons.Contains(episode.SeasonNumber))];
		}

		return [];
	}

	/// <summary>
	///     Match a daily series' single episode from an air date embedded in the file name, formats
	///     yyyy-MM-dd, yyyy.MM.dd, yyyy_MM_dd or yyyy MM dd.
	/// </summary>
	public static IReadOnlyList<Episode> MatchDaily(IReadOnlyCollection<Episode> episodes, string fileName)
	{
		var match = DailyDateRegex().Match(fileName);
		if (!match.Success)
		{
			return [];
		}

		if (!int.TryParse(match.Groups["y"].Value, out var year)
			|| !int.TryParse(match.Groups["m"].Value, out var month)
			|| !int.TryParse(match.Groups["d"].Value, out var day))
		{
			return [];
		}

		if (month is < 1 or > 12 || day is < 1 or > 31)
		{
			return [];
		}

		var airDate = $"{year:D4}-{month:D2}-{day:D2}";
		return [.. episodes.Where(episode => episode.AirDate == airDate)];
	}

	private static bool MatchesSeasonEpisode(Episode episode, int season, IReadOnlyList<int> episodeNumbers, SeriesNumbering numbering)
	{
		var episodeSeason = numbering == SeriesNumbering.AIRED ? episode.SeasonNumber : episode.SceneSeasonNumber ?? episode.SeasonNumber;
		var episodeNumber = numbering == SeriesNumbering.AIRED ? episode.EpisodeNumber : episode.SceneEpisodeNumber ?? episode.EpisodeNumber;

		return episodeSeason == season && episodeNumbers.Contains(episodeNumber);
	}

	[GeneratedRegex(@"(?<y>(?:19|20)\d{2})[.\-_ ](?<m>[01]?\d)[.\-_ ](?<d>[0-3]?\d)(?!\d)")]
	private static partial Regex DailyDateRegex();
}
