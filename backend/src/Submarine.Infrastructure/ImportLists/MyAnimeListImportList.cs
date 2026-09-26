using System.Text.Json;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     A MyAnimeList user list (watching, completed, on hold, dropped, plan to watch or all),
///     resolved to TVDB or TMDB ids by title.
/// </summary>
public sealed class MyAnimeListImportList(IHttpClientFactory httpClientFactory, IMappingsClient mappings, IMetadataClient metadata) : IImportList
{
	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.MYANIMELIST;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var accessToken = ImportListSettings.RequireString(settings, "accessToken", list.Name);
		var status = ImportListSettings.StringOr(settings, "listStatus", "all");

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		var url = "https://api.myanimelist.net/v2/users/@me/animelist?fields=list_status&limit=1000";
		if (status != "all")
		{
			url += $"&status={status}";
		}

		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.Add("Authorization", $"Bearer {accessToken}");
		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<ImportListItem>();
		if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
		{
			return items;
		}

		foreach (var entry in data.EnumerateArray())
		{
			if (!entry.TryGetProperty("node", out var node))
			{
				continue;
			}

			var title = node.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			cancellationToken.ThrowIfCancellationRequested();
			items.Add(await ResolveAsync(title, cancellationToken));
		}

		return items;
	}

	private async Task<ImportListItem> ResolveAsync(string title, CancellationToken cancellationToken)
	{
		foreach (var tvdbId in await mappings.FindByNameAsync(title, cancellationToken))
		{
			return new ImportListItem(tvdbId, null, null, title, null);
		}

		foreach (var hit in await metadata.SearchSeriesAsync(title, MetadataProvider.TMDB, cancellationToken))
		{
			if (hit.TmdbId is { } tmdbId && TitleNormalizer.CleanTitle(hit.Title) == TitleNormalizer.CleanTitle(title))
			{
				return new ImportListItem(null, tmdbId, null, title, hit.Year);
			}
		}

		return new ImportListItem(null, null, null, title, null);
	}
}
