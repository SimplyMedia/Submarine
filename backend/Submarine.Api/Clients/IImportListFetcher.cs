using Submarine.Core.ImportList;

namespace Submarine.Api.Clients;

/// <summary>
///     A single media item fetched from an import list source
/// </summary>
/// <param name="TvdbId">TheTVDB id of the item, if known</param>
/// <param name="TmdbId">TheMovieDB id of the item, if known</param>
/// <param name="AniListId">AniList id of the item, if known</param>
/// <param name="Title">title of the item</param>
/// <param name="IsSeries">whether the item is a series</param>
public record ImportListItem(int? TvdbId, int? TmdbId, int? AniListId, string Title, bool IsSeries);

/// <summary>
///     Fetches the items of an <see cref="ImportList" /> from its external source
/// </summary>
public interface IImportListFetcher
{
	/// <summary>
	///     Fetches all items of the given import list
	/// </summary>
	/// <param name="list">import list to fetch</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>items of the list, empty when the source is not configured</returns>
	Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default);
}
