using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.Indexers.Torznab;

/// <summary>
///     Parses Torznab and Newznab rss result feeds into <see cref="ReleaseInfo" /> items
/// </summary>
public static partial class TorznabFeedParser
{
	private static readonly XNamespace TorznabNamespace = "http://torznab.com/schemas/2015/feed";
	private static readonly XNamespace NewznabNamespace = "https://www.newznab.com/DTD/2010/feeds/attributes/";

	/// <summary>
	///     Parses a feed, skipping malformed items
	/// </summary>
	/// <param name="input">The feed xml string</param>
	/// <param name="protocol">The protocol assigned to the parsed releases</param>
	/// <returns>The parsed releases</returns>
	/// <exception cref="IndexerAuthException">When the feed reports wrong credentials</exception>
	/// <exception cref="IndexerException">When the feed reports another error</exception>
	public static IReadOnlyList<ReleaseInfo> Parse(string input, Protocol protocol)
	{
		var document = XElement.Parse(input);

		var error = document.Name.LocalName == "error" ? document : document.Descendants("error").FirstOrDefault();
		if (error is { })
		{
			var code = error.Attribute("code")?.Value;
			var description = error.Attribute("description")?.Value ?? "unknown indexer error";
			if (code is "100" or "101" or "102")
				throw new IndexerAuthException(description);
			throw new IndexerException($"{description} (code {code})");
		}

		var releases = new List<ReleaseInfo>();
		foreach (var item in document.Descendants("item"))
		{
			var release = ParseItem(item, protocol);
			if (release is { })
				releases.Add(release);
		}

		return releases;
	}

	private static ReleaseInfo? ParseItem(XElement item, Protocol protocol)
	{
		var attributes = item.Elements()
			.Where(element => element.Name.LocalName == "attr")
			.Select(element => (Name: element.Attribute("name")?.Value, Value: element.Attribute("value")?.Value))
			.Where(attr => attr is { Name: not null, Value: not null })
			.ToList();

		string? Attr(string name)
			=> attributes.FirstOrDefault(attr => attr.Name == name).Value;

		var title = item.Element("title")?.Value;
		var link = item.Element("link")?.Value;
		var enclosure = item.Element("enclosure");
		var enclosureUrl = enclosure?.Attribute("url")?.Value;
		var guid = item.Element("guid")?.Value ?? link ?? enclosureUrl;

		if (string.IsNullOrWhiteSpace(title) || guid is null)
			return null;

		var seeders = ParseInt(Attr("seeders"));
		var peers = ParseInt(Attr("peers"));

		var flags = new List<IndexerFlag>();
		var downloadFactor = ParseDouble(Attr("downloadvolumefactor"));
		var uploadFactor = ParseDouble(Attr("uploadvolumefactor"));
		if (downloadFactor is { } download)
		{
			switch (download)
			{
				case 0:
					flags.Add(IndexerFlag.FREELEECH);
					break;
				case 0.25:
					flags.Add(IndexerFlag.G_FREELEECH);
					break;
				case 0.5:
					flags.Add(IndexerFlag.HALFLEECH);
					break;
			}
		}

		if (uploadFactor is 2)
			flags.Add(IndexerFlag.DOUBLE_UPLOAD);

		var magnetUrl = Attr("magneturl");
		var infoHash = Attr("infohash")?.ToLowerInvariant();
		if (magnetUrl is { })
			infoHash ??= magnetUrl.Contains("btih:", StringComparison.OrdinalIgnoreCase)
				? magnetUrl[(magnetUrl.IndexOf("btih:", StringComparison.OrdinalIgnoreCase) + 5)..].Split('&')[0]
				: null;

		var categories = attributes
			.Where(attr => attr.Name == "category")
			.Select(attr => ParseInt(attr.Value))
			.OfType<int>()
			.ToList();

		return new ReleaseInfo
		{
			Title = title,
			Guid = guid,
			DownloadUrl = enclosureUrl ?? link,
			MagnetUrl = magnetUrl,
			InfoHash = infoHash,
			InfoUrl = item.Element("comments")?.Value,
			Size = ParseLong(Attr("size")) ?? ParseLong(enclosure?.Attribute("length")?.Value),
			PublishDate = ParseRfc1123(item.Element("pubDate")?.Value),
			Categories = categories,
			Seeders = seeders,
			Peers = peers,
			Leechers = ParseInt(Attr("leechers")) ?? (seeders.HasValue && peers.HasValue ? Math.Max(peers.Value - seeders.Value, 0) : null),
			Grabs = ParseInt(Attr("grabs")),
			Files = ParseInt(Attr("files")),
			DownloadVolumeFactor = downloadFactor ?? 1,
			UploadVolumeFactor = uploadFactor ?? 1,
			MinimumRatio = ParseDouble(Attr("minimumratio")),
			MinimumSeedTime = ParseInt(Attr("minimumseedtime")),
			Protocol = protocol,
			TvdbId = ParseInt(Attr("tvdbid")),
			TmdbId = ParseInt(Attr("tmdbid")),
			ImdbId = NormalizeImdbId(Attr("imdbid") ?? Attr("imdb")),
			IndexerFlags = flags
		};
	}

	private static DateTime? ParseRfc1123(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		// some feeds omit the colon in the utc offset, which .NET refuses to parse
		var normalized = Rfc1123OffsetRegex().Replace(value.Trim(), "$1:$2");
		if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var publishDate))
			return publishDate.UtcDateTime;

		// a leading weekday can also break parsing, retry without it
		var withoutWeekday = normalized.Substring(normalized.IndexOf(',') + 1).Trim();
		return DateTimeOffset.TryParse(withoutWeekday, CultureInfo.InvariantCulture, DateTimeStyles.None, out publishDate)
			? publishDate.UtcDateTime
			: null;
	}

	private static string? NormalizeImdbId(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim().StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? value.Trim() : $"tt{value.Trim()}";

	[GeneratedRegex(@"([+-]\d{2})(\d{2})$")]
	private static partial System.Text.RegularExpressions.Regex Rfc1123OffsetRegex();

	private static int? ParseInt(string? value)
		=> int.TryParse(value, out var parsed) ? parsed : null;

	private static long? ParseLong(string? value)
		=> long.TryParse(value, out var parsed) ? parsed : null;

	private static double? ParseDouble(string? value)
		=> double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
