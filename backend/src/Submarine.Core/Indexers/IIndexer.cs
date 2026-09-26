using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Submarine.Core.Provider;

namespace Submarine.Core.Indexers;

/// <summary>
///     A configured indexer that can be searched and downloaded from
/// </summary>
public interface IIndexer : IAsyncDisposable
{
	/// <summary>
	///     The display name of the indexer
	/// </summary>
	string Name { get; }

	/// <summary>
	///     The protocol releases of this indexer are distributed over
	/// </summary>
	Protocol Protocol { get; }

	/// <summary>
	///     Loads the search capabilities of the indexer
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The capabilities</returns>
	Task<IndexerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Performs a search
	/// </summary>
	/// <param name="request">The search to perform</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The found releases</returns>
	Task<IReadOnlyList<ReleaseInfo>> FetchAsync(SearchRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	///     Fetches the latest releases for an RSS sync
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The latest releases</returns>
	Task<IReadOnlyList<ReleaseInfo>> FetchRssAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Downloads a release link through the indexer, honoring proxy settings
	/// </summary>
	/// <param name="link">The download or magnet link reported by the indexer</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The response body</returns>
	Task<HttpResponseMessage> DownloadAsync(Uri link, CancellationToken cancellationToken = default);
}
