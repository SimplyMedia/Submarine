using Submarine.Core.Enums;
using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Plex watchlist via the plex.tv metadata service.
/// </summary>
public sealed class PlexImportList(IHttpClientFactory httpClientFactory, IMetadataClient metadata) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.PLEX;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var token = ImportListSettings.RequireString(settings, "accessToken", list.Name);

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var response = await client.GetAsync(
			$"https://metadata.provider.plex.tv/library/sections/watchlist/all?X-Plex-Token={Uri.EscapeDataString(token)}",
			cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		var metadataContainer = document.RootElement.GetProperty("MediaContainer").GetProperty("Metadata");
		foreach (var element in metadataContainer.EnumerateArray())
		{
			var title = element.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			var isShow = element.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "show";
			int? year = element.TryGetProperty("year", out var yearElement) && yearElement.TryGetInt32(out var y) ? y : null;
			items.Add(isShow
				? await ResolveSeriesAsync(title, cancellationToken)
				: await ResolveMovieAsync(title, year, cancellationToken));
		}

		return items;
	}

	private async Task<ImportListItem> ResolveMovieAsync(string title, int? year, CancellationToken cancellationToken)
	{
		foreach (var hit in await metadata.SearchMoviesAsync(title, year, cancellationToken))
		{
			if (hit.TmdbId is { } tmdbId)
			{
				return new ImportListItem(null, tmdbId, hit.ImdbId, title, year);
			}
		}

		return new ImportListItem(null, null, null, title, year);
	}

	private async Task<ImportListItem> ResolveSeriesAsync(string title, CancellationToken cancellationToken)
	{
		foreach (var hit in await metadata.SearchSeriesAsync(title, Core.Enums.MetadataProvider.TVDB, cancellationToken))
		{
			if (hit.TvdbId is { } tvdbId)
			{
				return new ImportListItem(tvdbId, null, null, title, hit.Year);
			}
		}

		return new ImportListItem(null, null, null, title, null);
	}
}
