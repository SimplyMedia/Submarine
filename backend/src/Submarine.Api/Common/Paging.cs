using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Submarine.Api.Common;

/// <summary>
///     Query string paging parameters: ?page=&amp;pageSize=&amp;sortKey=&amp;sortDirection=.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="SortKey">Property name to sort by, case-insensitive.</param>
/// <param name="SortDirection">ascending or descending, defaults to ascending.</param>
public sealed record PagingQuery(
	int Page = 1,
	int PageSize = 50,
	string? SortKey = null,
	string? SortDirection = null)
{
	/// <summary>Whether a sort direction string means descending. Accepts "descending" or "desc", case-insensitive; anything else, including null, is ascending.</summary>
	public static bool IsDescending(string? sortDirection)
		=> string.Equals(sortDirection, "descending", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
///     A single page of items.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
	private const int MaxPageSize = 500;

	/// <summary>
	///     Sort and page the source query. Works with EF Core queryables and in-memory queryables.
	///     No default order is applied when <see cref="PagingQuery.SortKey" /> is empty, since <typeparamref name="T" />
	///     is often an already-projected record here; pass an already-ordered source for a stable default, or use the
	///     overload taking a projector where defaulting to Id order is safe to translate.
	/// </summary>
	public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> source, PagingQuery query, CancellationToken cancellationToken = default)
	{
		var page = Math.Max(query.Page, 1);
		var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
		var sorted = ApplySort(source, query.SortKey, query.SortDirection);
		var totalCount = await CountAsync(sorted, cancellationToken);
		var items = await ToListAsync(sorted.Skip((page - 1) * pageSize).Take(pageSize), cancellationToken);
		return new PagedResult<T>(items, page, pageSize, totalCount);
	}

	/// <summary>
	///     Sort and page the source query, projecting only the final page into <typeparamref name="T" />.
	///     Sorting, counting and the default Id order (when no sort key is given) all run against
	///     <typeparamref name="TSource" /> (the entity) so EF Core can translate the ORDER BY;
	///     positional-record projections cannot be re-ordered after the fact.
	/// </summary>
	public static async Task<PagedResult<T>> CreateAsync<TSource>(
		IQueryable<TSource> source,
		PagingQuery query,
		Expression<Func<TSource, T>> projector,
		CancellationToken cancellationToken = default)
	{
		var page = Math.Max(query.Page, 1);
		var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
		var sorted = PagedResult<TSource>.ApplySort(source, query.SortKey, query.SortDirection, defaultToId: true);
		var totalCount = await CountAsync(sorted, cancellationToken);
		var items = await ToListAsync(
			sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(projector),
			cancellationToken);
		return new PagedResult<T>(items, page, pageSize, totalCount);
	}

	/// <summary>
	///     Order by a public property named by the sort key. Apply this to the entity query before
	///     projecting into a positional record; EF Core cannot order by constructor-bound members.
	///     Unknown sort keys throw a <see cref="ValidationException" /> (400); with
	///     <paramref name="defaultToId" /> set and no sort key given, falls back to ordering by Id
	///     when <typeparamref name="T" /> has one, otherwise leaves the source unsorted.
	/// </summary>
	public static IQueryable<T> ApplySort(IQueryable<T> source, string? sortKey, string? sortDirection, bool defaultToId = false)
	{
		if (string.IsNullOrWhiteSpace(sortKey))
		{
			if (!defaultToId)
			{
				return source;
			}

			var idProperty = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			return idProperty is null ? source : OrderBy(source, idProperty, descending: false);
		}

		var property = typeof(T).GetProperty(sortKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
			?? throw new ValidationException([
				new ValidationFailure("sortKey", $"Unknown sort key '{sortKey}' for {typeof(T).Name}")
			]);

		return OrderBy(source, property, PagingQuery.IsDescending(sortDirection));
	}

	private static IQueryable<T> OrderBy(IQueryable<T> source, PropertyInfo property, bool descending)
	{
		var parameter = Expression.Parameter(typeof(T), "x");
		var lambda = Expression.Lambda(Expression.Property(parameter, property), parameter);
		var methodName = descending ? "OrderByDescending" : "OrderBy";
		var call = Expression.Call(
			typeof(Queryable),
			methodName,
			[typeof(T), property.PropertyType],
			source.Expression,
			Expression.Quote(lambda));
		return source.Provider.CreateQuery<T>(call);
	}

	// EF Core queryables implement IAsyncEnumerable<T>; in-memory (LINQ to Objects) queryables do not,
	// so this dispatches to the async EF extensions only when the provider actually supports them.
	private static Task<int> CountAsync<TItem>(IQueryable<TItem> source, CancellationToken cancellationToken)
		=> source is IAsyncEnumerable<TItem> ? EntityFrameworkQueryableExtensions.CountAsync(source, cancellationToken) : Task.FromResult(source.Count());

	private static Task<List<TItem>> ToListAsync<TItem>(IQueryable<TItem> source, CancellationToken cancellationToken)
		=> source is IAsyncEnumerable<TItem> ? EntityFrameworkQueryableExtensions.ToListAsync(source, cancellationToken) : Task.FromResult(source.ToList());
}
