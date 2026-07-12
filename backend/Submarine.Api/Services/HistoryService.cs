using Microsoft.EntityFrameworkCore;
using Submarine.Api.Extensions;
using Submarine.Api.Models.Database;
using Submarine.Api.Models.Response;
using Submarine.Core.History;

namespace Submarine.Api.Services;

public class HistoryService
{
	private readonly SubmarineDatabaseContext _context;

	public HistoryService(SubmarineDatabaseContext context)
		=> _context = context;

	public virtual async Task RecordAsync(HistoryEvent @event, CancellationToken cancellationToken = default)
	{
		await _context.History.AddAsync(@event, cancellationToken);
		await _context.SaveChangesAsync(cancellationToken);
	}

	public Task<PagedResult<HistoryEvent>> GetPagedAsync(int page, int pageSize, HistoryEventType? type, int? seriesId,
		int? movieId)
	{
		var query = _context.History.AsNoTracking();

		if (type != null)
			query = query.Where(h => h.Type == type);

		if (seriesId != null)
			query = query.Where(h => h.SeriesId == seriesId);

		if (movieId != null)
			query = query.Where(h => h.MovieId == movieId);

		// SQLite can't translate DateTimeOffset ORDER BY; Id is monotonic with CreatedAt
		return query.OrderByDescending(h => h.Id).ToPagedResultAsync(page, pageSize);
	}
}
