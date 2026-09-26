using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     An IMDb list export (list id of the form "ls12345678"), resolved to TVDB or TMDB ids
///     depending on the list's media kind.
/// </summary>
public sealed class ImdbImportList(IHttpClientFactory httpClientFactory, IMetadataClient metadata) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.IMDB;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var listId = ImportListSettings.RequireString(settings, "listId", list.Name);

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var response = await client.GetAsync($"https://www.imdb.com/list/{listId}/export", cancellationToken);
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync(cancellationToken);

		var items = new List<ImportListItem>();
		var rows = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
		foreach (var row in rows.Skip(1))
		{
			var columns = row.Split(',');
			if (columns.Length <= 5)
			{
				continue;
			}

			var imdbId = columns[1];
			var title = columns[5];
			if (string.IsNullOrWhiteSpace(imdbId) || string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			cancellationToken.ThrowIfCancellationRequested();
			if (list.MediaKind == MediaKind.MOVIES)
			{
				var resolved = await metadata.GetMovieByImdbAsync(imdbId, cancellationToken);
				items.Add(new ImportListItem(null, resolved?.TmdbId, imdbId, resolved?.Title ?? title, resolved?.Year));
			}
			else
			{
				var hit = (await metadata.SearchSeriesAsync(title, MetadataProvider.TVDB, cancellationToken)).FirstOrDefault();
				items.Add(new ImportListItem(hit?.TvdbId, null, imdbId, hit?.Title ?? title, hit?.Year));
			}
		}

		return items;
	}
}
