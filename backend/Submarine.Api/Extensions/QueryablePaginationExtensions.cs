using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Response;

namespace Submarine.Api.Extensions;

public static class QueryablePaginationExtensions
{
	private const int DefaultPageSize = 50;
	private const int MaxPageSize = 250;

	public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> queryable, int page,
		int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
	{
		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

		var totalItems = await queryable.CountAsync(cancellationToken);

		var items = await queryable
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<T>(items, page, pageSize, totalItems);
	}
}
