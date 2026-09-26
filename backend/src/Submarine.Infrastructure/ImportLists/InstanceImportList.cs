using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Series or movies from another Sonarr or Radarr v3 instance.
/// </summary>
public sealed class InstanceImportList(IHttpClientFactory httpClientFactory) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type is ImportListType.SONARR or ImportListType.RADARR;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var baseUrl = ImportListSettings.RequireString(settings, "baseUrl", list.Name).TrimEnd('/');
		var apiKey = ImportListSettings.RequireString(settings, "apiKey", list.Name);

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			$"{baseUrl}/api/v3/{(list.Type == ImportListType.SONARR ? "series" : "movie")}");
		request.Headers.Add("X-Api-Key", apiKey);
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var idProperty = list.Type == ImportListType.SONARR ? "tvdbId" : "tmdbId";
		var items = new List<ImportListItem>();
		foreach (var element in document.RootElement.EnumerateArray())
		{
			var title = element.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			int? tvdbId = null, tmdbId = null, year = null;
			string? imdbId = null;
			if (element.TryGetProperty(idProperty, out var idElement) && idElement.TryGetInt32(out var id))
			{
				if (list.Type == ImportListType.SONARR)
				{
					tvdbId = id;
				}
				else
				{
					tmdbId = id;
				}
			}

			if (element.TryGetProperty("year", out var yearElement) && yearElement.TryGetInt32(out var y))
			{
				year = y;
			}

			if (element.TryGetProperty("imdbId", out var imdbElement) && imdbElement.ValueKind == JsonValueKind.String)
			{
				imdbId = imdbElement.GetString();
			}

			items.Add(new ImportListItem(tvdbId, tmdbId, imdbId, title, year));
		}

		return items;
	}
}
