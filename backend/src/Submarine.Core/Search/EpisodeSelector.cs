using Submarine.Core.Entities;
using Submarine.Core.Release;

namespace Submarine.Core.Search;

/// <summary>
///     Selects which episodes of a series a parsed release covers. Numbering is matched against each episode's aired
///     numbers first, falling back to its stored scene numbers.
/// </summary>
public static class EpisodeSelector
{
	/// <summary>
	///     Selects the episodes covered by the release.
	/// </summary>
	/// <param name="data">The parsed series release data, null when the release carries none.</param>
	/// <param name="episodes">Candidate episodes of the matched series.</param>
	/// <param name="dailyAirDate">The air date parsed from the release, for daily series; takes precedence over <paramref name="data" />.</param>
	public static IReadOnlyList<int> Select(SeriesReleaseData? data, IReadOnlyList<Episode> episodes, DateTime? dailyAirDate = null)
	{
		if (dailyAirDate is { } airDate)
		{
			return [.. episodes
				.Where(episode => episode.AirDateUtc is { } aired && aired.Date == airDate.Date)
				.Select(episode => episode.Id)];
		}

		if (data is null)
		{
			return [];
		}

		return data.ReleaseType switch
		{
			SeriesReleaseType.FULL_SEASON or SeriesReleaseType.MULTI_SEASON =>
				[.. episodes.Where(episode => MatchesSeason(episode, data.Seasons)).Select(episode => episode.Id)],

			SeriesReleaseType.SPECIAL =>
				[.. episodes.Where(episode => episode.SeasonNumber == 0
					&& (data.Episodes.Count == 0 || MatchesEpisode(episode, data.Episodes)))
					.Select(episode => episode.Id)],

			_ when data.AbsoluteEpisodes.Count > 0 =>
				[.. episodes.Where(episode => MatchesAbsolute(episode, data.AbsoluteEpisodes)).Select(episode => episode.Id)],

			SeriesReleaseType.EPISODE or SeriesReleaseType.MULTI_EPISODES or SeriesReleaseType.PARTIAL_SEASON =>
				[.. episodes
					.Where(episode => MatchesSeason(episode, data.Seasons) && MatchesEpisode(episode, data.Episodes))
					.Select(episode => episode.Id)],

			_ => []
		};
	}

	private static bool MatchesSeason(Episode episode, IReadOnlyList<int> seasons)
		=> seasons.Contains(episode.SeasonNumber)
		   || (episode.SceneSeasonNumber is { } sceneSeason && seasons.Contains(sceneSeason));

	private static bool MatchesEpisode(Episode episode, IReadOnlyList<int> episodeNumbers)
		=> episodeNumbers.Contains(episode.EpisodeNumber)
		   || (episode.SceneEpisodeNumber is { } sceneEpisode && episodeNumbers.Contains(sceneEpisode));

	private static bool MatchesAbsolute(Episode episode, IReadOnlyList<int> absoluteNumbers)
		=> (episode.AbsoluteEpisodeNumber is { } absolute && absoluteNumbers.Contains(absolute))
		   || (episode.SceneAbsoluteEpisodeNumber is { } sceneAbsolute && absoluteNumbers.Contains(sceneAbsolute));
}
