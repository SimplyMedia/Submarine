using Microsoft.AspNetCore.Http;

namespace Submarine.Api.Features.Compat.Shared;

public sealed record CompatPagedResult<T>(
	IReadOnlyList<T> Records,
	int TotalRecords,
	int Page,
	int PageSize,
	string SortKey,
	string SortDirection);

public readonly record struct CompatPageRequest(int Page, int PageSize, string SortKey, string SortDirection)
{
	public static CompatPageRequest FromQuery(IQueryCollection query)
		=> new(
			ReadPositive(query, "page", 1),
			ReadPositive(query, "pageSize", 10),
			query.TryGetValue("sortKey", out var sortKey) ? sortKey.ToString() : string.Empty,
			ReadSortDirection(query));

	private static string ReadSortDirection(IQueryCollection query)
	{
		var key = query.ContainsKey("sortDirection") ? "sortDirection" : "sortDir";
		if (!query.TryGetValue(key, out var direction))
			return "ascending";
		return direction.ToString().ToLowerInvariant() switch
		{
			"descending" or "desc" => "descending",
			"ascending" or "asc" => "ascending",
			_ => "ascending"
		};
	}

	public CompatPagedResult<T> Apply<T>(IEnumerable<T> source)
	{
		var records = source.ToArray();
		var totalRecords = records.Length;
		var offset = (long)(Page - 1) * PageSize;
		var pageRecords = offset >= totalRecords
			? Array.Empty<T>()
			: records.Skip((int)offset).Take(PageSize).ToArray();
		return new CompatPagedResult<T>(pageRecords, totalRecords, Page, PageSize, SortKey, SortDirection);
	}

	private static int ReadPositive(IQueryCollection query, string key, int defaultValue)
		=> query.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0
			? parsed
			: defaultValue;
}
