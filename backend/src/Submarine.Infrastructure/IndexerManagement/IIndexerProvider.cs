using Submarine.Core.Entities;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Builds live <see cref="IIndexer" /> instances from persisted indexer rows, resolving their proxy and Cardigann
///     definition, and caches their reported capabilities.
/// </summary>
public interface IIndexerProvider
{
	/// <summary>
	///     Loads every indexer enabled for the given search mode and not currently disabled by backoff, building a live
	///     client for each. Callers must dispose every returned client once done searching.
	/// </summary>
	Task<IReadOnlyList<ConfiguredIndexer>> GetEnabledAsync(IndexerSearchMode mode, CancellationToken cancellationToken = default);

	/// <summary>
	///     Builds a live client for one indexer row, resolving its proxy and Cardigann definition. The caller disposes it.
	/// </summary>
	Task<IIndexer> CreateAsync(Indexer entity, CancellationToken cancellationToken = default);

	/// <summary>
	///     Returns the indexer's search capabilities, from cache when the row's settings have not changed since the last
	///     fetch.
	/// </summary>
	Task<IndexerCapabilities> GetCapabilitiesAsync(Indexer entity, CancellationToken cancellationToken = default);

	/// <summary>
	///     Drops any cached capabilities of the indexer, forcing the next request to fetch them again.
	/// </summary>
	void InvalidateCapabilities(int indexerId);
}
