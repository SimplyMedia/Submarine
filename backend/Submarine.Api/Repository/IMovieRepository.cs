using Submarine.Core.Library;
using Submarine.Core.MediaFile;

namespace Submarine.Api.Repository;

/// <summary>
///     Movie Repository abstraction
/// </summary>
public interface IMovieRepository : IRepositoryBase<Movie>
{
	/// <summary>
	///     Finds a movie by id, including its versions
	/// </summary>
	/// <param name="id">id of the movie</param>
	/// <returns>movie with versions if found</returns>
	Task<Movie?> FindByIdWithVersionsAsync(int id);

	/// <summary>
	///     Finds the versions of the given movie
	/// </summary>
	/// <param name="movieId">id of the movie</param>
	/// <returns>versions of the movie</returns>
	Task<List<MediaVersion>> FindVersionsAsync(int movieId);

	/// <summary>
	///     Finds the movie file of the given movie in the given version
	/// </summary>
	/// <param name="movieId">id of the movie</param>
	/// <param name="versionId">id of the version</param>
	/// <returns>movie file if found</returns>
	Task<MovieFile?> FindMovieFileForVersionAsync(int movieId, int versionId);

	/// <summary>
	///     Finds all files of the given movie across versions
	/// </summary>
	/// <param name="movieId">id of the movie</param>
	/// <returns>movie files of the movie</returns>
	Task<List<MovieFile>> FindMovieFilesAsync(int movieId);

	/// <summary>
	///     Finds tracked movies by their ids, including versions, for mutation
	/// </summary>
	/// <param name="ids">ids of the movies</param>
	/// <returns>matching movies with versions</returns>
	Task<List<Movie>> FindByIdsWithVersionsAsync(IReadOnlyList<int> ids);
}
