using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Database;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;

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
	public Task<Episode?> FindEpisodeAsync(int id)
		=> DatabaseContext.Set<Episode>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

	/// <inheritdoc />
	public Task<EpisodeFile?> FindEpisodeFileAsync(int id)
		=> DatabaseContext.Set<EpisodeFile>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);

	/// <inheritdoc />
	public async Task<List<EpisodeFile>> FindEpisodeFilesBySeasonAsync(int seriesId, int seasonNumber)
	{
		var fileIds = await DatabaseContext.Set<Episode>().AsNoTracking()
			.Where(e => e.SeriesId == seriesId && e.SeasonNumber == seasonNumber && e.EpisodeFileId != null)
			.Select(e => e.EpisodeFileId!.Value)
			.ToListAsync();

		return await DatabaseContext.Set<EpisodeFile>().AsNoTracking()
			.Where(f => fileIds.Contains(f.Id))
			.ToListAsync();
	}

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
