using Submarine.Api.Models.Database;
using Submarine.Core.Library;

namespace Submarine.Api.Repository;

/// <summary>
///     Movie Repository implementation
/// </summary>
public class MovieRepository : RepositoryBase<Movie>, IMovieRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="MovieRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public MovieRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}
}
