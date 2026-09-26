using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Wraps a live <see cref="IIndexer" /> built by the indexer factory, stamping the Submarine indexer id and
///     configured name onto every returned release. The factory has no knowledge of the persisted indexer row, so it
///     cannot populate <see cref="ReleaseInfo.IndexerId" /> itself.
/// </summary>
/// <param name="inner">The underlying indexer client.</param>
/// <param name="indexerId">The Submarine id of the indexer row.</param>
/// <param name="indexerName">The configured name of the indexer row.</param>
internal sealed class StampedIndexer(IIndexer inner, int indexerId, string indexerName) : IIndexer
{
	/// <inheritdoc />
	public string Name => indexerName;

	/// <inheritdoc />
	public Core.Provider.Protocol Protocol => inner.Protocol;

	/// <inheritdoc />
	public Task<IndexerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
		=> inner.GetCapabilitiesAsync(cancellationToken);

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(SearchRequest request, CancellationToken cancellationToken = default)
		=> Stamp(await inner.FetchAsync(request, cancellationToken));

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReleaseInfo>> FetchRssAsync(CancellationToken cancellationToken = default)
		=> Stamp(await inner.FetchRssAsync(cancellationToken));

	/// <inheritdoc />
	public Task<HttpResponseMessage> DownloadAsync(Uri link, CancellationToken cancellationToken = default)
		=> inner.DownloadAsync(link, cancellationToken);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
		=> inner.DisposeAsync();

	private IReadOnlyList<ReleaseInfo> Stamp(IReadOnlyList<ReleaseInfo> releases)
		=> [.. releases.Select(release => release with { IndexerId = indexerId, Indexer = indexerName })];
}
