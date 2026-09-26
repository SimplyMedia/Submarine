using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Simkl user lists: watching, plan to watch, on hold, completed and dropped, for the main
///     catalogue (shows for series lists, movies for movie lists) or the anime catalogue.
/// </summary>
public sealed class SimklImportList(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.SIMKL;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var accessToken = ImportListSettings.RequireString(settings, "accessToken", list.Name);
		var clientId = ImportListSettings.String(settings, "clientId")
			?? configuration["Simkl:ClientId"]
			?? throw new InvalidOperationException($"Import list '{list.Name}' needs a Simkl client id");
		var listType = ImportListSettings.StringOr(settings, "listType", "watching") switch
		{
			"plan_to_watch" => "plantowatch",
			var other => other
		};
		var showType = ImportListSettings.StringOr(settings, "showType", "main");
		var catalogue = showType == "anime" ? "anime" : list.MediaKind == MediaKind.SERIES ? "shows" : "movies";

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.simkl.com/sync/all-items/{catalogue}/{listType}");
		request.Headers.Add("simkl-api-key", clientId);
		request.Headers.Add("Authorization", $"Bearer {accessToken}");
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		var propertyName = catalogue == "movies" ? "movie" : "show";
		var arrayName = catalogue == "anime" ? "anime" : catalogue;
		if (document.RootElement.TryGetProperty(arrayName, out var array) && array.ValueKind == JsonValueKind.Array)
		{
			foreach (var entry in array.EnumerateArray())
			{
				if (!entry.TryGetProperty(propertyName, out var media))
				{
					continue;
				}

				var title = media.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
				if (string.IsNullOrWhiteSpace(title))
				{
					continue;
				}

				int? year = media.TryGetProperty("year", out var yearElement) && yearElement.ValueKind == JsonValueKind.Number
					? yearElement.GetInt32()
					: null;
				var ids = media.TryGetProperty("ids", out var idsElement) ? idsElement : default;
				var imdbId = GetString(ids, "imdb");

				if (list.MediaKind == MediaKind.SERIES)
				{
					var tvdbId = GetInt(ids, "tvdb");
					if (tvdbId is null)
					{
						continue;
					}

					items.Add(new ImportListItem(tvdbId, null, imdbId, title, year));
				}
				else
				{
					var tmdbId = GetInt(ids, "tmdb");
					items.Add(new ImportListItem(null, tmdbId, imdbId, title, year));
				}
			}
		}

		return items;
	}

	private static string? GetString(JsonElement ids, string name)
		=> ids.ValueKind == JsonValueKind.Object && ids.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static int? GetInt(JsonElement ids, string name)
		=> GetString(ids, name) is { Length: > 0 } text && int.TryParse(text, out var number) ? number : null;
}
