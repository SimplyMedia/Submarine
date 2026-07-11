using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Submarine.Core.Parser;
using Submarine.Core.Provider;

namespace Submarine.Core.Indexer.Torznab;

/// <summary>
///     Service which parses Torznab/Newznab RSS result XML into <see cref="ReleaseInfo" /> items
/// </summary>
public class TorznabFeedParser : IParser<IReadOnlyList<ReleaseInfo>>
{
	private static readonly XNamespace TorznabNamespace = "http://torznab.com/schemas/2015/feed";

	private readonly ILogger<TorznabFeedParser> _logger;

	/// <summary>
	///     Creates a new <see cref="TorznabFeedParser" />
	/// </summary>
	/// <param name="logger">The logger of this <see cref="TorznabFeedParser" /></param>
	public TorznabFeedParser(ILogger<TorznabFeedParser> logger)
		=> _logger = logger;

	/// <summary>
	///     Parses RSS result XML, skipping malformed items
	/// </summary>
	/// <param name="input">The RSS XML string</param>
	/// <returns>The parsed <see cref="ReleaseInfo" /> items</returns>
	public IReadOnlyList<ReleaseInfo> Parse(string input)
	{
		var releases = new List<ReleaseInfo>();

		foreach (var item in XDocument.Parse(input).Descendants("item"))
		{
			var release = ParseItem(item);
			if (release != null) releases.Add(release);
		}

		return releases;
	}

	private ReleaseInfo? ParseItem(XElement item)
	{
		var attrs = item.Elements()
			.Where(element => element.Name.LocalName == "attr")
			.Select(element => (Name: element.Attribute("name")?.Value, Value: element.Attribute("value")?.Value))
			.Where(attr => attr is { Name: not null, Value: not null })
			.ToList();

		string? Attr(string name)
			=> attrs.FirstOrDefault(attr => attr.Name == name).Value;

		var title = item.Element("title")?.Value;
		var link = item.Element("link")?.Value;
		var enclosure = item.Element("enclosure");
		var enclosureUrl = enclosure?.Attribute("url")?.Value;
		var guid = item.Element("guid")?.Value ?? link ?? enclosureUrl;

		if (string.IsNullOrWhiteSpace(title) || guid == null)
		{
			_logger.LogWarning("Skipping malformed feed item without title or guid: {Item}", item);
			return null;
		}

		var seeders = ParseInt(Attr("seeders"));
		var peers = ParseInt(Attr("peers"));

		var isTorrent = item.Elements(TorznabNamespace + "attr").Any()
			|| enclosure?.Attribute("type")?.Value == "application/x-bittorrent";

		return new ReleaseInfo
		{
			Title = title,
			Guid = guid,
			DownloadUrl = enclosureUrl ?? link,
			InfoUrl = item.Element("comments")?.Value,
			Size = ParseLong(Attr("size")) ?? ParseLong(enclosure?.Attribute("length")?.Value),
			PublishDate = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture,
				DateTimeStyles.None, out var publishDate)
				? publishDate
				: null,
			Seeders = seeders,
			Peers = peers,
			Leechers = ParseInt(Attr("leechers")) ?? peers - seeders,
			IndexerFlags = ParseFlags(Attr("downloadvolumefactor"), Attr("uploadvolumefactor")),
			Categories = attrs.Where(attr => attr.Name == "category")
				.Select(attr => ParseInt(attr.Value))
				.OfType<int>()
				.ToList(),
			ImdbId = Attr("imdbid") ?? Attr("imdb"),
			TvdbId = ParseInt(Attr("tvdbid")),
			TmdbId = ParseInt(Attr("tmdbid")),
			Protocol = isTorrent ? Protocol.BITTORRENT : Protocol.USENET
		};
	}

	private static IReadOnlyList<IndexerFlag> ParseFlags(string? downloadVolumeFactor, string? uploadVolumeFactor)
	{
		var flags = new List<IndexerFlag>();

		if (ParseDouble(downloadVolumeFactor) is { } download)
		{
			if (download == 0) flags.Add(IndexerFlag.FREELEECH);
			else if (download == 0.5) flags.Add(IndexerFlag.HALFLEECH);
		}

		if (ParseDouble(uploadVolumeFactor) == 2) flags.Add(IndexerFlag.DOUBLE_UPLOAD);

		return flags;
	}

	private static int? ParseInt(string? value)
		=> int.TryParse(value, out var result) ? result : null;

	private static long? ParseLong(string? value)
		=> long.TryParse(value, out var result) ? result : null;

	private static double? ParseDouble(string? value)
		=> double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
}
