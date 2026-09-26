using System.Text;
using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Current season anime from the AniList GraphQL API, resolved to TVDB or TMDB ids.
/// </summary>
public sealed class AniListImportList(IHttpClientFactory httpClientFactory, IMappingsClient mappings, IMetadataClient metadata, TimeProvider timeProvider) : IImportList
{
	private const string Query = """
		query ($season: MediaSeason, $year: Int) {
			Page(perPage: 50) {
				media(season: $season, seasonYear: $year, type: ANIME, format: TV) {
					id
					title { romaji english }
				}
			}
		}
		""";

	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.ANILIST_SEASON;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		var now = timeProvider.GetUtcNow();
		var season = now.Month switch
		{
			12 or 1 or 2 => "WINTER",
			3 or 4 or 5 => "SPRING",
			6 or 7 or 8 => "SUMMER",
			_ => "FALL"
		};
		var year = now.Month == 12 ? now.Year + 1 : now.Year;

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var content = new StringContent(
			JsonSerializer.Serialize(new
			{
				query = Query,
				variables = new { season, year }
			}),
			Encoding.UTF8,
			"application/json");
		using var response = await client.PostAsync("https://graphql.anilist.co", content, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		foreach (var media in document.RootElement
			.GetProperty("data")
			.GetProperty("Page")
			.GetProperty("media")
			.EnumerateArray())
		{
			var title = media.GetProperty("title");
			var name = title.TryGetProperty("english", out var english) && english.ValueKind == JsonValueKind.String
				? english.GetString()
				: title.GetProperty("romaji").GetString();
			if (string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			items.Add(await ResolveAsync(name, cancellationToken));
		}

		return items;
	}

	private async Task<ImportListItem> ResolveAsync(string title, CancellationToken cancellationToken)
	{
		foreach (var tvdbId in await mappings.FindByNameAsync(title, cancellationToken))
		{
			return new ImportListItem(tvdbId, null, null, title, null);
		}

		foreach (var hit in await metadata.SearchSeriesAsync(title, Core.Enums.MetadataProvider.TMDB, cancellationToken))
		{
			if (hit.TmdbId is { } tmdbId && TitleNormalizer.CleanTitle(hit.Title) == TitleNormalizer.CleanTitle(title))
			{
				return new ImportListItem(null, tmdbId, null, title, hit.Year);
			}
		}

		return new ImportListItem(null, null, null, title, null);
	}
}
