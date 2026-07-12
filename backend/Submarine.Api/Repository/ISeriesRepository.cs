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
}
