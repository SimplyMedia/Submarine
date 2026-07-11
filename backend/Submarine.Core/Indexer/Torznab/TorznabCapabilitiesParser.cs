using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Submarine.Core.Parser;

namespace Submarine.Core.Indexer.Torznab;

/// <summary>
///     Service which parses Torznab/Newznab caps XML into <see cref="TorznabCapabilities" />
/// </summary>
public class TorznabCapabilitiesParser : IParser<TorznabCapabilities>
{
	/// <summary>
	///     Parses caps XML
	/// </summary>
	/// <param name="input">The caps XML string</param>
	/// <returns>The parsed <see cref="TorznabCapabilities" /></returns>
	/// <exception cref="InvalidOperationException">If the XML has no caps root element</exception>
	public TorznabCapabilities Parse(string input)
	{
		var caps = XDocument.Parse(input).Element("caps")
			?? throw new InvalidOperationException("Caps XML has no <caps> root element");

		var server = caps.Element("server");
		var limits = caps.Element("limits");
		var searching = caps.Element("searching");

		return new TorznabCapabilities
		{
			ServerTitle = server?.Attribute("title")?.Value,
			ServerVersion = server?.Attribute("version")?.Value,
			LimitsMax = ParseInt(limits?.Attribute("max")?.Value),
			LimitsDefault = ParseInt(limits?.Attribute("default")?.Value),
			Search = ParseSearchMode(searching?.Element("search")),
			TvSearch = ParseSearchMode(searching?.Element("tv-search")),
			MovieSearch = ParseSearchMode(searching?.Element("movie-search")),
			Categories = caps.Element("categories")?.Elements("category").Select(ParseCategory).ToList()
				?? new List<TorznabCategory>()
		};
	}

	private static TorznabSearchMode? ParseSearchMode(XElement? element)
		=> element == null
			? null
			: new TorznabSearchMode
			{
				Available = element.Attribute("available")?.Value == "yes",
				SupportedParams = element.Attribute("supportedParams")?.Value
					?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []
			};

	private static TorznabCategory ParseCategory(XElement element)
		=> new()
		{
			Id = int.Parse(element.Attribute("id")?.Value
				?? throw new InvalidOperationException("Category element has no id attribute")),
			Name = element.Attribute("name")?.Value ?? string.Empty,
			Subcategories = element.Elements("subcat").Select(ParseCategory).ToList()
		};

	private static int? ParseInt(string? value)
		=> int.TryParse(value, out var result) ? result : null;
}
