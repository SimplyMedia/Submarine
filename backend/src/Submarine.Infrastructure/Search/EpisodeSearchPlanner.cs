using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Search;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Builds the indexer requests for one episode, choosing daily, anime absolute or standard aired numbering and
///     resolving scene numbering when the episode does not already carry a stored scene number.
/// </summary>
/// <param name="mappingsClient">Resolves aired numbering into scene numbering.</param>
public sealed class EpisodeSearchPlanner(IMappingsClient mappingsClient)
{
	/// <summary>
	///     Builds the request(s) needed to search for the episode; more than one when the series is anime and both
	///     absolute and standard aired forms should be tried.
	/// </summary>
	public async Task<IReadOnlyList<SearchRequest>> BuildAsync(Series series, Episode episode, CancellationToken cancellationToken = default)
	{
		if (series.Type == SeriesType.DAILY && episode.AirDateUtc is { } airDate)
		{
			return [SearchRequestBuilder.BuildDailyEpisodeQuery(series.Title, airDate, series.TvdbId, series.TmdbId, series.ImdbId)];
		}

		var (season, episodeNumber) = await ResolveSceneNumberingAsync(series, episode, cancellationToken);

		if (series.Type == SeriesType.ANIME && episode.AbsoluteEpisodeNumber is { } absolute)
		{
			return SearchRequestBuilder.BuildAnimeEpisodeQueries(
				series.Title, season, episodeNumber, absolute, series.TvdbId, series.TmdbId, series.ImdbId, animeStandardFormatSearch: true);
		}

		return [SearchRequestBuilder.BuildStandardEpisodeQuery(series.Title, season, episodeNumber, series.TvdbId, series.TmdbId, series.ImdbId)];
	}

	/// <summary>
	///     Resolves the season and episode number to search with: the episode's stored scene numbering when present,
	///     otherwise a live lookup, falling back to the aired numbering when the mappings service is unreachable.
	/// </summary>
	public async Task<(int Season, int Episode)> ResolveSceneNumberingAsync(Series series, Episode episode, CancellationToken cancellationToken = default)
	{
		if (episode.SceneSeasonNumber is { } sceneSeason && episode.SceneEpisodeNumber is { } sceneEpisode)
		{
			return (sceneSeason, sceneEpisode);
		}

		try
		{
			var resolution = await mappingsClient.ResolveSceneAsync(series.TvdbId, episode.SeasonNumber, episode.EpisodeNumber, cancellationToken);
			return (resolution.SceneSeason, resolution.SceneEpisode);
		}
		catch (HttpRequestException)
		{
			return (episode.SeasonNumber, episode.EpisodeNumber);
		}
	}
}
