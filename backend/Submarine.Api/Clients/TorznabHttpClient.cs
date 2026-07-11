using Submarine.Core.Download;
using Submarine.Core.Indexer;
using Submarine.Core.Indexer.Torznab;
using Submarine.Core.Parser;
using Submarine.Core.Provider;

namespace Submarine.Api.Clients;

/// <summary>
///     Talks to Torznab/Newznab indexers over http, building queries and parsing responses
/// </summary>
public class TorznabHttpClient
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IParser<TorznabCapabilities> _capabilitiesParser;
	private readonly IParser<IReadOnlyList<ReleaseInfo>> _feedParser;

	public TorznabHttpClient(IHttpClientFactory httpClientFactory,
		IParser<TorznabCapabilities> capabilitiesParser, IParser<IReadOnlyList<ReleaseInfo>> feedParser)
	{
		_httpClientFactory = httpClientFactory;
		_capabilitiesParser = capabilitiesParser;
		_feedParser = feedParser;
	}

	/// <summary>
	///     Fetches the capabilities of an indexer
	/// </summary>
	public async Task<TorznabCapabilities> GetCapabilitiesAsync(Provider indexer,
		CancellationToken cancellationToken = default)
		=> _capabilitiesParser.Parse(
			await FetchAsync(indexer, new TorznabRequestBuilder(indexer.ApiKey).BuildCapsQuery(), cancellationToken));

	/// <summary>
	///     Runs a tv search against an indexer
	/// </summary>
	public Task<IReadOnlyList<ReleaseInfo>> TvSearchAsync(Provider indexer, int? tvdbId = null, int? season = null,
		int? episode = null, string? query = null, CancellationToken cancellationToken = default)
		=> ParseFeedAsync(indexer,
			new TorznabRequestBuilder(indexer.ApiKey).BuildTvSearchQuery(query, season, episode, tvdbId),
			cancellationToken);

	/// <summary>
	///     Runs a movie search against an indexer
	/// </summary>
	public Task<IReadOnlyList<ReleaseInfo>> MovieSearchAsync(Provider indexer, int? tmdbId = null, string? imdbId = null,
		string? query = null, CancellationToken cancellationToken = default)
		=> ParseFeedAsync(indexer,
			new TorznabRequestBuilder(indexer.ApiKey).BuildMovieSearchQuery(query, imdbId, tmdbId), cancellationToken);

	/// <summary>
	///     Runs a free text search against an indexer
	/// </summary>
	public Task<IReadOnlyList<ReleaseInfo>> SearchAsync(Provider indexer, string? query = null,
		IReadOnlyList<int>? categories = null, CancellationToken cancellationToken = default)
		=> ParseFeedAsync(indexer, new TorznabRequestBuilder(indexer.ApiKey).BuildSearchQuery(query, categories),
			cancellationToken);

	private async Task<IReadOnlyList<ReleaseInfo>> ParseFeedAsync(Provider indexer, string query,
		CancellationToken cancellationToken)
		=> _feedParser.Parse(await FetchAsync(indexer, query, cancellationToken))
			.Select(release => release with { Protocol = indexer.Protocol })
			.ToList();

	private async Task<string> FetchAsync(Provider indexer, string query, CancellationToken cancellationToken)
	{
		var client = _httpClientFactory.CreateClient("indexer");

		try
		{
			var response = await client.GetAsync(new Uri(new Uri(indexer.Url), query), cancellationToken);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new DownloadClientException($"Request to indexer {indexer.Name} failed: {ex.Message}", ex);
		}
	}
}
