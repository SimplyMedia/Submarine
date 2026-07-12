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
		=> Query().Include(s => s.Seasons).Include(s => s.Versions).FirstOrDefaultAsync(s => s.Id == id);

	/// <inheritdoc />
	public IQueryable<Episode> QueryEpisodes(int seriesId)
		=> DatabaseContext.Set<Episode>().AsNoTracking().Where(e => e.SeriesId == seriesId);

	/// <inheritdoc />
	public Task<Episode?> FindEpisodeAsync(int id)
		=> DatabaseContext.Set<Episode>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

	/// <inheritdoc />
	public Task<EpisodeFile?> FindEpisodeFileForVersionAsync(int episodeId, int versionId)
		=> DatabaseContext.Set<EpisodeFile>().AsNoTracking()
			.FirstOrDefaultAsync(f => f.MediaVersionId == versionId && f.Episodes.Any(e => e.Id == episodeId));

	/// <inheritdoc />
	public Task<List<EpisodeFile>> FindEpisodeFilesBySeasonAsync(int seriesId, int seasonNumber, int versionId)
		=> DatabaseContext.Set<EpisodeFile>().AsNoTracking()
			.Where(f => f.SeriesId == seriesId && f.MediaVersionId == versionId
			                                   && f.Episodes.Any(e => e.SeasonNumber == seasonNumber))
			.ToListAsync();

	/// <inheritdoc />
	public Task<List<MediaVersion>> FindVersionsAsync(int seriesId)
		=> DatabaseContext.Set<MediaVersion>().AsNoTracking()
			.Where(v => v.SeriesId == seriesId)
			.OrderBy(v => v.Id)
			.ToListAsync();

	/// <inheritdoc />
	public Task<List<EpisodeFile>> FindEpisodeFilesAsync(int seriesId)
		=> DatabaseContext.Set<EpisodeFile>().AsNoTracking()
			.Where(f => f.SeriesId == seriesId)
			.ToListAsync();

	/// <inheritdoc />
	public async Task<List<Episode>> FindEpisodesByAirDateAsync(DateTimeOffset start, DateTimeOffset end)
	{
		// SQLite can't translate DateTimeOffset comparison operators, so the range filter runs in memory
		var episodes = await DatabaseContext.Set<Episode>().AsNoTracking()
			.Include(e => e.Files)
			.Where(e => e.AirDate != null)
			.ToListAsync();

		return episodes.Where(e => e.AirDate >= start && e.AirDate <= end).ToList();
	}
}
