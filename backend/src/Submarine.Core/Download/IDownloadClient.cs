namespace Submarine.Core.Download;

/// <summary>
///     A download client Submarine can send releases to and monitor
/// </summary>
public interface IDownloadClient
{
	/// <summary>
	///     Client type this instance talks to
	/// </summary>
	DownloadClientType Type { get; }

	/// <summary>
	///     Protocol this client downloads
	/// </summary>
	Provider.Protocol Protocol { get; }

	/// <summary>
	///     Sends a release to the client
	/// </summary>
	/// <param name="release">Release to download</param>
	/// <param name="seedCriteria">Seeding limits; clients apply what their API supports and ignore the rest</param>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	/// <returns>The client-side download id; the upper-case info hash for torrents</returns>
	/// <exception cref="DownloadClientException">The release could not be added</exception>
	Task<string> AddAsync(RemoteRelease release, SeedCriteria? seedCriteria, CancellationToken cancellationToken);

	/// <summary>
	///     Lists downloads currently known to the client
	/// </summary>
	Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken);

	/// <summary>
	///     Reports the folders the client downloads into
	/// </summary>
	Task<DownloadClientStatus> GetStatusAsync(CancellationToken cancellationToken);

	/// <summary>
	///     Removes a download from the client
	/// </summary>
	/// <param name="downloadId">Client-side download id</param>
	/// <param name="deleteData">Whether downloaded data is deleted as well</param>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	Task RemoveAsync(string downloadId, bool deleteData, CancellationToken cancellationToken);

	/// <summary>
	///     Client-specific post-import bookkeeping, e.g. switching the qBittorrent category; most clients no-op
	/// </summary>
	Task MarkImportedAsync(string downloadId, CancellationToken cancellationToken);

	/// <summary>
	///     Verifies the client is reachable and credentials work
	/// </summary>
	/// <exception cref="DownloadClientException">Thrown with an actionable message when the client is unreachable or rejects the configuration</exception>
	Task TestAsync(CancellationToken cancellationToken);
}
