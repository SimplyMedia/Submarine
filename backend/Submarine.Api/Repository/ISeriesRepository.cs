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
	///     Finds an episode file by its id
	/// </summary>
	/// <param name="id">id of the episode file</param>
	/// <returns>episode file if found</returns>
	Task<EpisodeFile?> FindEpisodeFileAsync(int id);

	/// <summary>
	///     Finds the episode files satisfying episodes of the given series and season
	/// </summary>
	/// <param name="seriesId">id of the series</param>
	/// <param name="seasonNumber">season number</param>
	/// <returns>matching episode files</returns>
	Task<List<EpisodeFile>> FindEpisodeFilesBySeasonAsync(int seriesId, int seasonNumber);
	/// <summary>
	///     Finds a series by id, including its seasons
	/// </summary>
	/// <param name="id">id of the series</param>
	/// <returns>series with seasons if found</returns>
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
}
