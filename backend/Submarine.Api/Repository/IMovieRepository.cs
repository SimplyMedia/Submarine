using Submarine.Core.Library;
using Submarine.Core.MediaFile;

namespace Submarine.Api.Repository;

/// <summary>
///     Movie Repository abstraction
/// </summary>
public interface IMovieRepository : IRepositoryBase<Movie>
{
	/// <summary>
	///     Finds a movie file by its id
	/// </summary>
	/// <param name="id">id of the movie file</param>
	/// <returns>movie file if found</returns>
	Task<MovieFile?> FindMovieFileAsync(int id);
}
