using Submarine.Core.Library;
using Submarine.Core.MediaFile;

namespace Submarine.Api.Repository;

/// <summary>
///     Series Repository abstraction
/// </summary>
public interface ISeriesRepository : IRepositoryBase<Series>
{
	/// <summary>
	///     Finds an episode by its id
	/// </summary>
	/// <param name="id">id of the episode</param>
	/// <returns>episode if found</returns>
	Task<Episode?> FindEpisodeAsync(int id);

	/// <summary>
	///     Finds the episode file satisfying the given episode in the given version
	/// </summary>
	/// <param name="episodeId">id of the episode</param>
	/// <param name="versionId">id of the version</param>
	/// <returns>episode file if found</returns>
	Task<EpisodeFile?> FindEpisodeFileForVersionAsync(int episodeId, int versionId);

	/// <summary>
	///     Finds the episode files satisfying episodes of the given series and season within a version
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <param name="seasonNumber">season number</param>
	/// <param name="versionId">id of the version</param>
	/// <returns>matching episode files</returns>
	Task<List<EpisodeFile>> FindEpisodeFilesBySeasonAsync(int seriesId, int seasonNumber, int versionId);

	/// <summary>
	///     Finds the versions of the given series
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <returns>versions of the series</returns>
	Task<List<MediaVersion>> FindVersionsAsync(int seriesId);

	/// <summary>
	///     Finds all episode files of the given series across versions
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <returns>episode files of the series</returns>
	Task<List<EpisodeFile>> FindEpisodeFilesAsync(int seriesId);

	/// <summary>
	///     Finds a series by id, including its seasons and versions
	/// </summary>
	/// <param name="id">id of the series</param>
	/// <returns>series with seasons and versions if found</returns>
	Task<Series?> FindByIdWithSeasonsAsync(int id);

	/// <summary>
	///     Queries episodes belonging to the given series
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <returns>queryable of episodes</returns>
	IQueryable<Episode> QueryEpisodes(int seriesId);

	/// <summary>
	///     Finds episodes with an air date within the given range
	/// </summary>
	/// <param name="start">start of the range, inclusive</param>
	/// <param name="end">end of the range, inclusive</param>
	/// <returns>matching episodes</returns>
	Task<List<Episode>> FindEpisodesByAirDateAsync(DateTimeOffset start, DateTimeOffset end);

	/// <summary>
	///     Finds tracked episodes by their ids, for mutation
	/// </summary>
	/// <param name="ids">ids of the episodes</param>
	/// <returns>matching episodes</returns>
	Task<List<Episode>> FindEpisodesByIdsAsync(IReadOnlyList<int> ids);

	/// <summary>
	///     Finds a season of a series
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <param name="seasonNumber">number of the season</param>
	/// <returns>season if found</returns>
	Task<Season?> FindSeasonAsync(int seriesId, int seasonNumber);

	/// <summary>
	///     Finds tracked episodes of a season, for mutation
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <param name="seasonNumber">number of the season</param>
	/// <returns>episodes of the season</returns>
	Task<List<Episode>> FindEpisodesForSeasonAsync(int seriesId, int seasonNumber);

	/// <summary>
	///     Persists the monitored state of a season together with its episodes in a single save
	/// </summary>
	/// <param name="season">season to persist</param>
	/// <param name="episodes">episodes to persist</param>
	Task SaveSeasonWithEpisodesAsync(Season season, IEnumerable<Episode> episodes);

	/// <summary>
	///     Persists the given episodes
	/// </summary>
	/// <param name="episodes">episodes to persist</param>
	Task SaveEpisodesAsync(IEnumerable<Episode> episodes);

	/// <summary>
	///     Finds tracked series by their ids, including versions, for mutation
	/// </summary>
	/// <param name="ids">ids of the series</param>
	/// <returns>matching series with versions</returns>
	Task<List<Series>> FindByIdsWithVersionsAsync(IReadOnlyList<int> ids);
}
