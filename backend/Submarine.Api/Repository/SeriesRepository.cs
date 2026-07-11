using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Core.Library;

namespace Submarine.Api.Repository;

/// <summary>
///     Series Repository implementation
/// </summary>
public class SeriesRepository : RepositoryBase<Series>, ISeriesRepository
{
	/// <summary>
	///     Creates a new instance of <see cref="SeriesRepository" />
	/// </summary>
	/// <param name="databaseContext">Database Context</param>
	public SeriesRepository(SubmarineDatabaseContext databaseContext) : base(databaseContext)
	{
	}

	/// <inheritdoc />
	public Task<Series?> FindByIdWithSeasonsAsync(int id)
		=> Query().Include(s => s.Seasons).FirstOrDefaultAsync(s => s.Id == id);

	/// <inheritdoc />
	public IQueryable<Episode> QueryEpisodes(int seriesId)
		=> DatabaseContext.Set<Episode>().AsNoTracking().Where(e => e.SeriesId == seriesId);

	/// <inheritdoc />
	public async Task<List<Episode>> FindEpisodesByAirDateAsync(DateTimeOffset start, DateTimeOffset end)
	{
		// SQLite can't translate DateTimeOffset comparison operators, so the range filter runs in memory
		var episodes = await DatabaseContext.Set<Episode>().AsNoTracking()
			.Where(e => e.AirDate != null)
			.ToListAsync();

		return episodes.Where(e => e.AirDate >= start && e.AirDate <= end).ToList();
	}
}
