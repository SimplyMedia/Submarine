using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     One item fetched from an import list.
/// </summary>
/// <param name="TvdbId">TVDB id, when known.</param>
/// <param name="TmdbId">TMDB id, when known.</param>
/// <param name="ImdbId">IMDB id, when known.</param>
/// <param name="Title">Title of the series or movie.</param>
/// <param name="Year">Release year, when known.</param>
public sealed record ImportListItem(int? TvdbId, int? TmdbId, string? ImdbId, string Title, int? Year);

/// <summary>
///     Fetches items for the import list types it handles.
/// </summary>
public interface IImportList
{
	/// <summary>Whether this implementation handles the type.</summary>
	bool Handles(ImportListType type);

	/// <summary>
	///     Fetch the current items of the list.
	/// </summary>
	/// <exception cref="InvalidOperationException">The list settings are incomplete or the source failed.</exception>
	Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default);
}
