using System.Text.RegularExpressions;
using System.Xml.Linq;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     A generic RSS/XML feed. Series feeds need a numeric TVDB id in each item's guid element.
///     Movie feeds are parsed by title, with an IMDb id extracted from the item's link when present.
/// </summary>
public sealed partial class RssImportList(IHttpClientFactory httpClientFactory, IMetadataClient metadata) : IImportList
{
	[GeneratedRegex(@"tt\d{7,9}")]
	private static partial Regex ImdbIdPattern();

	/// <inheritdoc />
	public bool Handles(ImportListType type) => type == ImportListType.RSS;

	/// <inheritdoc />
	public async Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list, CancellationToken cancellationToken = default)
	{
		using var settings = ImportListSettings.Parse(list);
		var url = ImportListSettings.RequireString(settings, "url", list.Name);

		var client = httpClientFactory.CreateClient("SubmarineImportLists");
		var content = await client.GetStringAsync(url, cancellationToken);
		var document = XDocument.Parse(content);
		var entries = document.Root?.Element("channel")?.Elements("item") ?? [];

		var items = new List<ImportListItem>();
		foreach (var entry in entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var title = entry.Element("title")?.Value;
			if (string.IsNullOrWhiteSpace(title))
			{
				continue;
			}

			if (list.MediaKind == MediaKind.SERIES)
			{
				var guid = entry.Element("guid")?.Value;
				if (guid is null || !int.TryParse(guid, out var tvdbId))
				{
					throw new InvalidOperationException($"Import list '{list.Name}': item '{title}' has no numeric TVDB guid");
				}

				items.Add(new ImportListItem(tvdbId, null, null, title, null));
				continue;
			}

			if (title.Contains("TV Series", StringComparison.OrdinalIgnoreCase)
				|| title.Contains("Mini-Series", StringComparison.OrdinalIgnoreCase)
				|| title.Contains("TV Episode", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var link = entry.Element("link")?.Value ?? string.Empty;
			var imdbMatch = ImdbIdPattern().Match(link) is { Success: true } match ? match.Value : null;
			if (imdbMatch is null)
			{
				continue;
			}

			var resolved = await metadata.GetMovieByImdbAsync(imdbMatch, cancellationToken);
			if (resolved is null)
			{
				continue;
			}

			items.Add(new ImportListItem(null, resolved.TmdbId, imdbMatch, resolved.Title, resolved.Year));
		}

		return items;
	}
}
