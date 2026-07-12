using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;

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

	/// <inheritdoc />
	public Task<Movie?> FindByIdWithVersionsAsync(int id)
		=> Query().Include(m => m.Versions).FirstOrDefaultAsync(m => m.Id == id);

	/// <inheritdoc />
	public Task<List<MediaVersion>> FindVersionsAsync(int movieId)
		=> DatabaseContext.Set<MediaVersion>().AsNoTracking()
			.Where(v => v.MovieId == movieId)
			.OrderBy(v => v.Id)
			.ToListAsync();

	/// <inheritdoc />
	public Task<MovieFile?> FindMovieFileForVersionAsync(int movieId, int versionId)
		=> DatabaseContext.Set<MovieFile>().AsNoTracking()
			.FirstOrDefaultAsync(f => f.MovieId == movieId && f.MediaVersionId == versionId);

	/// <inheritdoc />
	public Task<List<MovieFile>> FindMovieFilesAsync(int movieId)
		=> DatabaseContext.Set<MovieFile>().AsNoTracking()
			.Where(f => f.MovieId == movieId)
			.ToListAsync();
}
