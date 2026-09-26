using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Trakt lists: public lists, trending and popular, and user watchlists, collections and watched.
/// </summary>
public sealed class TraktImportList(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type)
		=> type is ImportListType.TRAKT_LIST or ImportListType.TRAKT_POPULAR or ImportListType.TRAKT_USER;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var clientId = ImportListSettings.String(settings, "clientId")
			?? configuration["Trakt:ClientId"]
			?? throw new InvalidOperationException($"Import list '{list.Name}' needs a Trakt client id");
		var path = list.Type switch
		{
			ImportListType.TRAKT_LIST => $"users/{ImportListSettings.RequireString(settings, "user", list.Name)}/lists/{ImportListSettings.RequireString(settings, "list", list.Name)}/items/{Segment(list)}",
			ImportListType.TRAKT_POPULAR => $"{Segment(list)}/{ImportListSettings.StringOr(settings, "category", "trending")}",
			_ => list.MediaKind == MediaKind.MOVIES
				? $"users/{ImportListSettings.RequireString(settings, "user", list.Name)}/{WatchlistPath(ImportListSettings.RequireString(settings, "listType", list.Name))}"
				: $"users/{ImportListSettings.RequireString(settings, "user", list.Name)}/{WatchlistPath(ImportListSettings.RequireString(settings, "listType", list.Name))}/{Segment(list)}",
		};

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.trakt.tv/{path}");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		request.Headers.Add("trakt-api-version", "2");
		request.Headers.Add("trakt-api-key", clientId);
		if (ImportListSettings.String(settings, "accessToken") is { Length: > 0 } token)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		foreach (var element in document.RootElement.EnumerateArray())
		{
			var node = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("movie", out var movie)
				? movie
				: element.ValueKind == JsonValueKind.Object && element.TryGetProperty("show", out var show)
					? show
					: element;
			if (node.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var title = node.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
				? titleElement.GetString()
				: null;
			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			int? tmdbId = null, year = null;
			string? imdbId = null;
			if (node.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Object)
			{
				tmdbId = ids.TryGetProperty("tmdb", out var tmdb) && tmdb.TryGetInt32(out var t) ? t : null;
				imdbId = ids.TryGetProperty("imdb", out var imdb) && imdb.ValueKind == JsonValueKind.String ? imdb.GetString() : null;
			}

			if (node.TryGetProperty("year", out var yearElement) && yearElement.TryGetInt32(out var y))
			{
				year = y;
			}

			items.Add(new ImportListItem(null, tmdbId, imdbId, title, year));
		}

		return items;
	}

	private static string Segment(ImportList list)
		=> list.MediaKind == MediaKind.MOVIES ? "movies" : "shows";

	private static string WatchlistPath(string listType)
		=> listType switch
		{
			"watchlist" => "watchlist",
			"collection" => "collection",
			"watched" => "watched",
			_ => throw new InvalidOperationException($"Unknown Trakt list type '{listType}'")
		};
}
