using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     StevenLu movie list and custom JSON lists returning objects with tmdbId or tvdbId and title.
/// </summary>
public sealed class JsonFeedImportList(IHttpClientFactory httpClientFactory, IMetadataClient metadata) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type is ImportListType.STEVEN_LU or ImportListType.CUSTOM;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var url = list.Type == ImportListType.STEVEN_LU
			? ImportListSettings.StringOr(settings, "url", "https://stevenlu.com/movies.json")
			: ImportListSettings.RequireString(settings, "url", list.Name);

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var response = await client.GetAsync(url, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		foreach (var element in document.RootElement.EnumerateArray())
		{
			if (element.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var title = GetString(element, "title");
			int? tmdbId = GetInt(element, "tmdbId") ?? GetInt(element, "tmdb_id");
			int? tvdbId = GetInt(element, "tvdbId") ?? GetInt(element, "tvdb_id");
			var imdbId = GetString(element, "imdbId") ?? GetString(element, "imdb_id");
			int? year = GetInt(element, "year");

			if (tmdbId is null && tvdbId is null && imdbId is not null)
			{
				var resolved = await metadata.GetMovieByImdbAsync(imdbId, cancellationToken);
				tmdbId = resolved?.TmdbId;
				title ??= resolved?.Title;
			}

			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			items.Add(new ImportListItem(tvdbId, tmdbId, imdbId, title, year));
		}

		return items;
	}

	private static string? GetString(JsonElement element, string name)
		=> element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static int? GetInt(JsonElement element, string name)
		=> element.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? number
				: null;
}
