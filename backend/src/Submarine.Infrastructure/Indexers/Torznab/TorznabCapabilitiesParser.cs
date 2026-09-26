using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.Indexers.Torznab;

/// <summary>
///     Parses Torznab and Newznab caps xml into <see cref="IndexerCapabilities" />
/// </summary>
public static class TorznabCapabilitiesParser
{
	/// <summary>
	///     Parses a caps document
	/// </summary>
	/// <param name="input">The caps xml string</param>
	/// <returns>The capabilities</returns>
	/// <exception cref="IndexerException">When the xml has no caps root element</exception>
	public static IndexerCapabilities Parse(string input)
	{
		var document = XElement.Parse(input);
		var caps = document.Name.LocalName == "caps" ? document : document.Element("caps")
			?? throw new IndexerException("Caps xml has no <caps> root element");

		var limits = caps.Element("limits");
		var searching = caps.Element("searching");

		var basic = ParseMode(searching?.Element("search"));
		var tv = ParseMode(searching?.Element("tv-search"));
		var movie = ParseMode(searching?.Element("movie-search"));
		var music = ParseMode(searching?.Element("music-search"));
		var book = ParseMode(searching?.Element("book-search"));

		var categories = caps.Element("categories")?.Elements("category")
			.Select(ParseCategory)
			.ToList() ?? [];

		return new IndexerCapabilities
		{
			SearchAvailable = basic?.Available ?? false,
			SearchParams = basic?.Params ?? SearchParams.Q,
			TvSearchAvailable = tv?.Available ?? false,
			TvSearchParams = tv?.Params ?? SearchParams.Q,
			MovieSearchAvailable = movie?.Available ?? false,
			MovieSearchParams = movie?.Params ?? SearchParams.Q,
			MusicSearchAvailable = music?.Available ?? false,
			MusicSearchParams = music?.Params ?? SearchParams.Q,
			BookSearchAvailable = book?.Available ?? false,
			BookSearchParams = book?.Params ?? SearchParams.Q,
			Categories = categories,
			LimitsMax = ParseInt(limits?.Attribute("max")?.Value),
			LimitsDefault = ParseInt(limits?.Attribute("default")?.Value),
			SupportsRawSearch = true
		};
	}

	private sealed record SearchMode(bool Available, SearchParams Params);

	private static SearchMode? ParseMode(XElement? element)
		=> element is null
			? null
			: new SearchMode(
				element.Attribute("available")?.Value == "yes",
				element.Attribute("supportedParams")?.Value
					?.Split(',', StringSplitOptions.RemoveEmptyEntries)
					.Aggregate(
						SearchParams.None,
						(current, parameter) => current | parameter.Trim().ToLowerInvariant() switch
						{
							"q" => SearchParams.Q,
							"season" => SearchParams.Season,
							"ep" => SearchParams.Ep,
							"imdbid" or "imdbidshort" => SearchParams.ImdbId,
							"tmdbid" => SearchParams.TmdbId,
							"tvdbid" => SearchParams.TvdbId,
							"rid" => SearchParams.Rid,
							"year" => SearchParams.Year,
							"genre" => SearchParams.Genre,
							"artist" => SearchParams.Artist,
							"album" => SearchParams.Album,
							"label" => SearchParams.Label,
							"track" => SearchParams.Track,
							"author" => SearchParams.Author,
							"title" => SearchParams.Title,
							"publisher" => SearchParams.Publisher,
							_ => SearchParams.None
						}) ?? SearchParams.None);

	private static IndexerCategory ParseCategory(XElement element)
	{
		var id = int.Parse(element.Attribute("id")?.Value
			?? throw new IndexerException("Category element has no id attribute"), System.Globalization.CultureInfo.InvariantCulture);
		var name = element.Attribute("name")?.Value ?? IndexerCategories.ById(id)?.Name ?? id.ToString();
		var subcategories = element.Elements("subcat").Select(ParseCategory).ToList();
		return new IndexerCategory(id, name, subcategories);
	}

	private static int? ParseInt(string? value)
		=> int.TryParse(value, out var parsed) ? parsed : null;
}
