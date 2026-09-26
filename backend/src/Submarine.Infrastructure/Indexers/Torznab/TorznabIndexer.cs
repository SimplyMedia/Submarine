using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;

namespace Submarine.Infrastructure.Indexers.Torznab;

/// <summary>
///     Shared implementation of the Torznab and Newznab wire protocol
/// </summary>
/// <param name="name">The indexer name</param>
/// <param name="indexerId">The Submarine id of this indexer</param>
/// <param name="priority">The priority of this indexer</param>
/// <param name="protocol">The protocol releases are distributed over</param>
/// <param name="baseUrl">The base url of the indexer</param>
/// <param name="apiPath">The api path</param>
/// <param name="apiKey">The api key</param>
/// <param name="categories">The categories to search</param>
/// <param name="additionalParameters">Extra raw query parameters</param>
/// <param name="http">The http access</param>
/// <param name="logger">The logger</param>
public abstract class TorznabCompatibleIndexer(
	string name,
	int? indexerId,
	int priority,
	Protocol protocol,
	string baseUrl,
	string apiPath,
	string? apiKey,
	IReadOnlyList<int>? categories,
	string? additionalParameters,
	IIndexerHttpClient http,
	ILogger logger) : IIndexer
{
	private IndexerCapabilities? _capabilities;

	/// <inheritdoc />
	public string Name { get; } = name;

	/// <inheritdoc />
	public Protocol Protocol { get; } = protocol;

	/// <summary>
	///     The request categories of this indexer instance
	/// </summary>
	protected IReadOnlyList<int> Categories { get; } = categories ?? [];

	private string BaseUrl { get; } = baseUrl;

	private string ApiPath { get; } = apiPath;

	/// <inheritdoc />
	public async Task<IndexerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		if (_capabilities is { } cached)
			return cached;

		var builder = new TorznabRequestBuilder(apiKey);
		var uri = TorznabRequestBuilder.ToUri(BaseUrl, ApiPath, builder.BuildCapsQuery());
		logger.LogDebug("Fetching capabilities of {Name} from {Uri}", Name, uri);
		var response = await http.GetAsync(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (response.StatusCode == HttpStatusCode.Unauthorized)
			throw new IndexerAuthException($"{Name} rejected the api key");

		_capabilities = TorznabCapabilitiesParser.Parse(body);
		return _capabilities;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(SearchRequest request, CancellationToken cancellationToken = default)
	{
		var capabilities = await GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
		var categories = request.Categories is { Count: > 0 }
			? request.Categories
			: Categories;

		var builder = new TorznabRequestBuilder(apiKey);
		var query = builder.BuildSearchQuery(request with { Categories = categories }, additionalParameters);
		var uri = TorznabRequestBuilder.ToUri(BaseUrl, ApiPath, query);
		logger.LogDebug("Searching {Name} at {Uri}", Name, uri);

		var response = await http.GetAsync(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
			throw new IndexerException($"{Name} returned {(int)response.StatusCode} for {uri}");

		return ApplyIndexerInfo(TorznabFeedParser.Parse(body, Protocol))
			.Where(release => IsSupported(request, release, capabilities))
			.ToList();
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ReleaseInfo>> FetchRssAsync(CancellationToken cancellationToken = default)
		=> FetchAsync(new BasicSearchRequest(Categories: Categories, IsRss: true), cancellationToken);

	/// <inheritdoc />
	public Task<HttpResponseMessage> DownloadAsync(Uri link, CancellationToken cancellationToken = default)
		=> http.GetAsync(link, cancellationToken: cancellationToken);

	private IReadOnlyList<ReleaseInfo> ApplyIndexerInfo(IReadOnlyList<ReleaseInfo> releases)
		=> releases
			.Select(release => release with
			{
				IndexerId = indexerId,
				Indexer = Name,
				IndexerPriority = priority
			})
			.ToList();

	private static bool IsSupported(SearchRequest request, ReleaseInfo release, IndexerCapabilities capabilities)
	{
		// indexers report unsupported search modes as empty results, a mismatch here means the
		// release does not belong to the requested categories at all
		if (request.Categories is not { Count: > 0 } requested)
			return true;

		var wanted = requested
			.SelectMany(id => (IReadOnlyList<int>)IndexerCategories.InclusiveDescendantIds(id))
			.ToHashSet();
		return release.Categories.Count == 0 || release.Categories.Any(wanted.Contains);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		http.Dispose();
		return ValueTask.CompletedTask;
	}
}

/// <summary>
///     A Torznab indexer
/// </summary>
/// <param name="settings">The settings of this indexer instance</param>
/// <param name="http">The http access</param>
/// <param name="logger">The logger</param>
public sealed class TorznabIndexer(
	TorznabSettings settings,
	IIndexerHttpClient http,
	ILogger<TorznabIndexer> logger) : TorznabCompatibleIndexer(
		ResolveName(settings),
		null,
		25,
		Protocol.BITTORRENT,
		settings.BaseUrl,
		settings.ApiPath,
		settings.ApiKey,
		MergeCategories(settings.Categories, settings.AnimeCategories),
		settings.AdditionalParameters,
		http,
		logger)
{
	private static string ResolveName(TorznabSettings settings)
		=> Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : settings.BaseUrl;

	private static IReadOnlyList<int> MergeCategories(IReadOnlyList<int>? categories, IReadOnlyList<int>? animeCategories)
		=> [.. (categories ?? []).Union(animeCategories ?? [])];
}

/// <summary>
///     A Newznab indexer
/// </summary>
/// <param name="settings">The settings of this indexer instance</param>
/// <param name="http">The http access</param>
/// <param name="logger">The logger</param>
public sealed class NewznabIndexer(
	NewznabSettings settings,
	IIndexerHttpClient http,
	ILogger<NewznabIndexer> logger) : TorznabCompatibleIndexer(
		ResolveName(settings),
		null,
		25,
		Protocol.USENET,
		settings.BaseUrl,
		settings.ApiPath,
		settings.ApiKey,
		MergeCategories(settings.Categories, settings.AnimeCategories),
		settings.AdditionalParameters,
		http,
		logger)
{
	private static string ResolveName(NewznabSettings settings)
		=> Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : settings.BaseUrl;

	private static IReadOnlyList<int> MergeCategories(IReadOnlyList<int>? categories, IReadOnlyList<int>? animeCategories)
		=> [.. (categories ?? []).Union(animeCategories ?? [])];
}
