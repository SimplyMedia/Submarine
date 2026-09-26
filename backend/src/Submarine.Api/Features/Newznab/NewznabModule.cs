using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Submarine.Api.Modules;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;

namespace Submarine.Api.Features.Newznab;

/// <summary>
///     Outbound Newznab/Torznab API so external tools (or Submarine instances) can use Submarine's configured
///     indexers as a single upstream indexer, plus the download proxy every rendered result links through.
/// </summary>
public sealed class NewznabModule : IEndpointModule
{
	private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
	private static readonly XNamespace Torznab = "http://torznab.com/schemas/2015/feed";
	private static readonly XNamespace Newznab = "http://www.newznab.com/DTD/2010/feeds/attributes/1.0/";
	private static readonly NewznabRateLimiter RateLimiter = new();

	/// <inheritdoc />
	public void Map(IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/api/v1/indexer/{id:int}/newznab/api", SingleAsync).AllowAnonymous();
		endpoints.MapGet("/api/v1/indexers/newznab/api", AggregateAsync).AllowAnonymous();
		endpoints.MapGet("/api/v1/indexers/torznab/api", AggregateAsync).AllowAnonymous();
		endpoints.MapGet("/api/v1/indexer/{id:int}/download", DownloadAsync).AllowAnonymous();
	}

	private static async Task<IResult> SingleAsync(
		int id,
		HttpRequest httpRequest,
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		[AsParameters] ReleaseSearchQuery query,
		CancellationToken cancellationToken)
	{
		if (!await AuthorizeAsync(db, httpRequest, cancellationToken))
		{
			return XmlResult(BuildError(100, "Invalid API Key"), 401);
		}

		if (!RateLimiter.TryAcquire(query.ApiKey ?? "anonymous"))
		{
			return Results.StatusCode(429);
		}

		if (!TryValidateCategories(query.Cat))
		{
			return XmlResult(BuildError(201, "Incorrect parameter"), 400);
		}

		var indexer = await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return XmlResult(BuildError(200, "Indexer not found"), 404);
		}

		var baseUrl = BaseUrl(httpRequest);
		await using var client = await indexerProvider.CreateAsync(indexer, cancellationToken);
		try
		{
			if (query.Type == "caps")
			{
				var capabilities = await client.GetCapabilitiesAsync(cancellationToken);
				return XmlResult(BuildCaps([capabilities]));
			}

			var request = BuildRequest(query);
			var releases = await client.FetchAsync(request, cancellationToken);
			await RecordAsync(db, id, query, cancellationToken);
			return XmlResult(BuildFeed(releases, query.ApiKey ?? string.Empty, baseUrl));
		}
		catch (IndexerException exception)
		{
			return XmlResult(BuildError(900, exception.Message), 502);
		}
	}

	private static async Task<IResult> AggregateAsync(
		HttpRequest httpRequest,
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		[AsParameters] ReleaseSearchQuery query,
		CancellationToken cancellationToken)
	{
		if (!await AuthorizeAsync(db, httpRequest, cancellationToken))
		{
			return XmlResult(BuildError(100, "Invalid API Key"), 401);
		}

		if (!RateLimiter.TryAcquire(query.ApiKey ?? "anonymous"))
		{
			return Results.StatusCode(429);
		}

		if (!TryValidateCategories(query.Cat))
		{
			return XmlResult(BuildError(201, "Incorrect parameter"), 400);
		}

		var baseUrl = BaseUrl(httpRequest);
		var indexers = await indexerProvider.GetEnabledAsync(IndexerSearchMode.AUTOMATIC, cancellationToken);
		try
		{
			if (query.Type == "caps")
			{
				var capabilities = await Task.WhenAll(indexers.Select(configured => configured.Client.GetCapabilitiesAsync(cancellationToken)));
				return XmlResult(BuildCaps(capabilities));
			}

			var request = BuildRequest(query);
			var batches = await Task.WhenAll(indexers.Select(async configured =>
			{
				try
				{
					return await configured.Client.FetchAsync(request, cancellationToken);
				}
				catch (IndexerException)
				{
					return (IReadOnlyList<ReleaseInfo>)[];
				}
			}));

			await RecordAsync(db, null, query, cancellationToken);
			return XmlResult(BuildFeed([.. batches.SelectMany(batch => batch)], query.ApiKey ?? string.Empty, baseUrl));
		}
		finally
		{
			foreach (var configured in indexers)
			{
				await configured.Client.DisposeAsync();
			}
		}
	}

	private static async Task<IResult> DownloadAsync(
		int id,
		[FromQuery] string link,
		[FromQuery] string? file,
		[FromQuery(Name = "apikey")] string? apikey,
		SubmarineDbContext db,
		IIndexerProvider indexerProvider,
		CancellationToken cancellationToken)
	{
		var config = await db.GeneralConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
		if (config is null || !FixedTimeEquals(apikey ?? string.Empty, config.ApiKey))
		{
			return Results.Unauthorized();
		}

		string decoded;
		try
		{
			decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(link));
		}
		catch (FormatException)
		{
			return Results.BadRequest();
		}

		if (!Uri.TryCreate(decoded, UriKind.Absolute, out var decodedUri))
		{
			return Results.BadRequest();
		}

		if (string.Equals(decodedUri.Scheme, "magnet", StringComparison.OrdinalIgnoreCase))
		{
			return Results.Redirect(decoded, permanent: false);
		}

		var indexer = await db.Indexers.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
		if (indexer is null)
		{
			return Results.NotFound();
		}

		var definition = indexer.DefinitionId is null
			? null
			: await db.IndexerDefinitions.AsNoTracking().FirstOrDefaultAsync(entity => entity.DefinitionId == indexer.DefinitionId, cancellationToken);
		if (!IsAllowedDownloadHost(decodedUri, indexer, definition))
		{
			return Results.BadRequest();
		}

		await using var client = await indexerProvider.CreateAsync(indexer, cancellationToken);
		HttpResponseMessage response;
		try
		{
			response = await client.DownloadAsync(decodedUri, cancellationToken);
		}
		catch (IndexerException)
		{
			return Results.BadRequest();
		}

		if (response.Headers.Location is { } redirect)
		{
			return Results.Redirect(redirect.ToString(), permanent: false);
		}

		var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		var contentType = response.Content.Headers.ContentType?.MediaType
			?? (indexer.Protocol == Protocol.USENET ? "application/x-nzb" : "application/x-bittorrent");
		var fileName = string.IsNullOrWhiteSpace(file) ? $"{id}" : file;
		return Results.File(bytes, contentType, fileName);
	}

	// Only follow the link through the indexer's own authenticated client when it points back at that
	// indexer's configured host (or one of its Cardigann definition's known hosts); otherwise the proxy
	// would fetch and leak indexer credentials/cookies to an attacker-controlled host (SSRF).
	private static bool IsAllowedDownloadHost(Uri target, Indexer indexer, IndexerDefinition? definition)
	{
		if (Uri.TryCreate(indexer.BaseUrl, UriKind.Absolute, out var baseUri)
			&& string.Equals(baseUri.Host, target.Host, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return definition is not null && definition.Links.Any(link =>
			Uri.TryCreate(link, UriKind.Absolute, out var linkUri)
			&& string.Equals(linkUri.Host, target.Host, StringComparison.OrdinalIgnoreCase));
	}

	private static async Task<bool> AuthorizeAsync(SubmarineDbContext db, HttpRequest request, CancellationToken cancellationToken)
	{
		var apiKey = request.Query["apikey"].FirstOrDefault();
		if (string.IsNullOrEmpty(apiKey))
		{
			return false;
		}

		var config = await db.GeneralConfig.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
		return config is not null && FixedTimeEquals(apiKey, config.ApiKey);
	}

	private static bool FixedTimeEquals(string left, string right)
	{
		var leftBytes = Encoding.UTF8.GetBytes(left);
		var rightBytes = Encoding.UTF8.GetBytes(right);
		return leftBytes.Length == rightBytes.Length
			&& System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
	}

	private static string BaseUrl(HttpRequest request)
		=> $"{request.Scheme}://{request.Host}{request.PathBase}";

	private static async Task RecordAsync(SubmarineDbContext db, int? indexerId, ReleaseSearchQuery query, CancellationToken cancellationToken)
	{
		var userAgent = "unknown";
		if (indexerId is not null)
		{
			db.IndexerHistories.Add(new IndexerHistory
			{
				IndexerId = indexerId.Value,
				EventType = IndexerHistoryEventType.QUERY,
				Successful = true,
				Query = query.Q,
				Categories = query.Cat,
				Source = $"newznab:{userAgent}",
				Date = DateTime.UtcNow
			});
			await db.SaveChangesAsync(cancellationToken);
		}
	}

	// Defence in depth: BuildRequest also parses cat defensively, but callers should reject an
	// invalid cat before doing any indexer work so it never surfaces as a 500.
	private static bool TryValidateCategories(string? cat)
		=> string.IsNullOrWhiteSpace(cat) || cat.Split(',', StringSplitOptions.RemoveEmptyEntries).All(part => int.TryParse(part, out _));

	internal static SearchRequest BuildRequest(ReleaseSearchQuery query)
	{
		List<int>? categories = null;
		if (!string.IsNullOrWhiteSpace(query.Cat))
		{
			categories = [];
			foreach (var part in query.Cat.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				if (int.TryParse(part, out var value))
				{
					categories.Add(value);
				}
			}
		}

		return query.Type switch
		{
			"tvsearch" => new TvSearchRequest(
				query.Q, query.Season, query.Ep, query.TvdbId, query.TmdbId, query.ImdbId,
				Categories: categories, Limit: query.Limit, Offset: query.Offset),
			"movie" => new MovieSearchRequest(query.Q, ImdbId: query.ImdbId, TmdbId: query.TmdbId, Categories: categories, Limit: query.Limit, Offset: query.Offset),
			_ => new BasicSearchRequest(query.Q, categories, query.Limit, query.Offset)
		};
	}

	private static IResult XmlResult(XDocument document, int statusCode = 200)
		=> Results.Text(document.Declaration + document.ToString(SaveOptions.DisableFormatting), "application/rss+xml", Encoding.UTF8, statusCode);

	internal static XDocument BuildError(int code, string description)
		=> new(new XDeclaration("1.0", "UTF-8", null), new XElement("error", new XAttribute("code", code), new XAttribute("description", description)));

	internal static XDocument BuildCaps(IReadOnlyCollection<IndexerCapabilities> capabilities)
	{
		var categories = capabilities.SelectMany(capability => capability.Categories).DistinctBy(category => category.Id).ToList();
		if (categories.Count == 0)
		{
			categories = [.. IndexerCategories.All];
		}

		return new XDocument(
			new XDeclaration("1.0", "UTF-8", null),
			new XElement(
				"caps",
				new XElement(
					"searching",
					new XElement("search", new XAttribute("available", capabilities.Any(c => c.SearchAvailable) ? "yes" : "no"), new XAttribute("supportedParams", "q")),
					new XElement("tv-search", new XAttribute("available", capabilities.Any(c => c.TvSearchAvailable) ? "yes" : "no"), new XAttribute("supportedParams", "q,season,ep,tvdbid,tmdbid,imdbid")),
					new XElement("movie-search", new XAttribute("available", capabilities.Any(c => c.MovieSearchAvailable) ? "yes" : "no"), new XAttribute("supportedParams", "q,imdbid,tmdbid,year"))),
				new XElement("categories", categories.Select(CategoryElement))));
	}

	private static XElement CategoryElement(IndexerCategory category)
		=> new(
			"category",
			new XAttribute("id", category.Id),
			new XAttribute("name", category.Name),
			category.SubCategories.Select(sub => new XElement("subcat", new XAttribute("id", sub.Id), new XAttribute("name", sub.Name))));

	internal static XDocument BuildFeed(IReadOnlyList<ReleaseInfo> releases, string apiKey, string baseUrl = "")
		=> new(
			new XDeclaration("1.0", "UTF-8", null),
			new XElement(
				"rss",
				new XAttribute("version", "2.0"),
				new XAttribute(XNamespace.Xmlns + "atom", Atom.NamespaceName),
				new XAttribute(XNamespace.Xmlns + "torznab", Torznab.NamespaceName),
				new XAttribute(XNamespace.Xmlns + "newznab", Newznab.NamespaceName),
				new XElement(
					"channel",
					new XElement("title", "Submarine"),
					releases.Select(release => ItemElement(release, apiKey, baseUrl)))));

	private static XElement ItemElement(ReleaseInfo release, string apiKey, string baseUrl)
	{
		var downloadLink = release.MagnetUrl ?? release.DownloadUrl;
		var enclosureUrl = downloadLink is null || release.IndexerId is null
			? null
			: $"{baseUrl}/api/v1/indexer/{release.IndexerId}/download?link={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(downloadLink))}&file={Uri.EscapeDataString(release.Title)}&apikey={apiKey}";

		var attrs = new List<XElement?>
		{
			Attr("size", release.Size),
			Attr("seeders", release.Seeders),
			Attr("peers", release.Peers ?? (release.Seeders + release.Leechers)),
			Attr("infohash", release.InfoHash),
			Attr("magneturl", release.MagnetUrl),
			Attr("downloadvolumefactor", release.DownloadVolumeFactor),
			Attr("uploadvolumefactor", release.UploadVolumeFactor),
			Attr("tvdbid", release.TvdbId),
			Attr("imdbid", release.ImdbId),
			Attr("category", release.Categories.Count > 0 ? string.Join(",", release.Categories) : null)
		};

		var enclosureType = release.Protocol == Protocol.USENET ? "application/x-nzb" : "application/x-bittorrent";
		return new XElement(
			"item",
			new XElement("title", release.Title),
			new XElement("guid", release.Guid),
			release.InfoUrl is null ? null : new XElement("link", release.InfoUrl),
			new XElement("pubDate", (release.PublishDate ?? DateTime.UtcNow).ToString("R")),
			new XElement("size", release.Size ?? 0),
			enclosureUrl is null ? null : new XElement("enclosure", new XAttribute("url", enclosureUrl), new XAttribute("length", release.Size ?? 0), new XAttribute("type", enclosureType)),
			attrs.Where(attr => attr is not null));
	}

	private static XElement? Attr(string name, object? value)
		=> value is null ? null : new XElement(Torznab + "attr", new XAttribute("name", name), new XAttribute("value", value.ToString()!));
}

/// <summary>Bound Newznab/Torznab query parameters.</summary>
/// <param name="Type">t: caps, search, tvsearch, movie, music or book.</param>
/// <param name="Q">Free text query.</param>
/// <param name="Season">Season number.</param>
/// <param name="Ep">Episode number.</param>
/// <param name="TvdbId">TheTVDB id.</param>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="ImdbId">IMDb id.</param>
/// <param name="Cat">Comma separated standard category ids.</param>
/// <param name="Limit">Maximum amount of results.</param>
/// <param name="Offset">Offset into the results.</param>
/// <param name="ApiKey">The caller's api key.</param>
public sealed record ReleaseSearchQuery(
	[FromQuery(Name = "t")] string Type,
	[FromQuery] string? Q,
	[FromQuery] int? Season,
	[FromQuery] int? Ep,
	[FromQuery(Name = "tvdbid")] int? TvdbId,
	[FromQuery(Name = "tmdbid")] int? TmdbId,
	[FromQuery(Name = "imdbid")] string? ImdbId,
	[FromQuery] string? Cat,
	[FromQuery] int? Limit,
	[FromQuery] int? Offset,
	[FromQuery(Name = "apikey")] string? ApiKey);

/// <summary>
///     Fixed one minute window rate limiter keyed by api key, 60 requests per window.
/// </summary>
internal sealed class NewznabRateLimiter
{
	private const int Limit = 60;
	private readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _windows = new();

	public bool TryAcquire(string key)
	{
		var now = DateTime.UtcNow;
		var updated = _windows.AddOrUpdate(
			key,
			_ => (now, 1),
			(_, existing) => now - existing.WindowStart > TimeSpan.FromMinutes(1) ? (now, 1) : (existing.WindowStart, existing.Count + 1));

		return updated.Count <= Limit;
	}
}
