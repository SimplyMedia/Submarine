namespace Submarine.Api.Models.Response;

/// <summary>
///     A page of results from a larger collection
/// </summary>
/// <typeparam name="T">Type of the items in this page</typeparam>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);
