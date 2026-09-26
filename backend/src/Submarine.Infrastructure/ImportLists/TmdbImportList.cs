using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Import lists served by the Metadata service: TMDB lists, popular, collections and persons.
/// </summary>
public sealed class TmdbImportList(IMetadataClient metadata) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type)
		=> type is ImportListType.TMDB_LIST or ImportListType.TMDB_POPULAR or ImportListType.TMDB_COLLECTION or ImportListType.TMDB_PERSON;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		return list.Type switch
		{
			ImportListType.TMDB_LIST => await FetchMovies(
				await metadata.GetTmdbListAsync(ImportListSettings.RequireInt(settings, "listId", list.Name), cancellationToken),
				cancellationToken),
			ImportListType.TMDB_POPULAR => await FetchPopular(cancellationToken),
			ImportListType.TMDB_COLLECTION => await FetchCollection(list, settings, cancellationToken),
			_ => await FetchPerson(list, settings, cancellationToken)
		};
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchPopular(CancellationToken cancellationToken)
	{
		var items = new List<ImportListItem>();
		foreach (var page in Enumerable.Range(1, 3))
		{
			foreach (var hit in await metadata.GetPopularMoviesAsync(page, cancellationToken))
			{
				items.Add(new ImportListItem(null, hit.TmdbId, hit.ImdbId, hit.Title, hit.Year));
			}
		}

		return items;
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchCollection(ImportList list, System.Text.Json.JsonDocument settings, CancellationToken cancellationToken)
	{
		var collection = await metadata.GetCollectionAsync(ImportListSettings.RequireInt(settings, "collectionId", list.Name), cancellationToken)
			?? throw new InvalidOperationException($"Collection not found for import list '{list.Name}'");
		return await FetchMovies(collection.Movies, cancellationToken);
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchPerson(ImportList list, System.Text.Json.JsonDocument settings, CancellationToken cancellationToken)
	{
		var movies = await metadata.GetPersonMoviesAsync(ImportListSettings.RequireInt(settings, "personId", list.Name), cancellationToken);
		return await FetchMovies(movies, cancellationToken);
	}

	private async Task<IReadOnlyList<ImportListItem>> FetchMovies(IReadOnlyList<Contracts.Metadata.MovieResource> movies, CancellationToken cancellationToken)
	{
		var items = new List<ImportListItem>();
		foreach (var movie in movies)
		{
			if (movie.TmdbId > 0)
			{
				items.Add(new ImportListItem(null, movie.TmdbId, movie.ImdbId, movie.Title, movie.Year));
			}
			else if (movie.ImdbId is not null)
			{
				var resolved = await metadata.GetMovieByImdbAsync(movie.ImdbId, cancellationToken);
				if (resolved is not null)
				{
					items.Add(new ImportListItem(null, resolved.TmdbId, movie.ImdbId, resolved.Title, resolved.Year));
				}
			}
		}

		return items;
	}
}
