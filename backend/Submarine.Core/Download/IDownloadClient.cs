using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Core.Download;

/// <summary>
///     A download client Submarine can send releases to and monitor
/// </summary>
public interface IDownloadClient
{
	/// <summary>
	///     Protocol this client downloads
	/// </summary>
	Protocol Protocol { get; }

	/// <summary>
	///     Sends a release to the client
	/// </summary>
	/// <param name="release">Release to download</param>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	/// <returns>The client-side download id</returns>
	Task<string> AddDownloadAsync(ReleaseInfo release, CancellationToken cancellationToken = default);

	/// <summary>
	///     Lists downloads currently known to the client
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Removes a download from the client
	/// </summary>
	/// <param name="downloadId">Client-side download id</param>
	/// <param name="deleteData">Whether downloaded data is deleted as well</param>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	Task RemoveItemAsync(string downloadId, bool deleteData, CancellationToken cancellationToken = default);

	/// <summary>
	///     Verifies the client is reachable and credentials work
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	/// <exception cref="DownloadClientException">Thrown when the client is unreachable or rejects the configuration</exception>
	Task TestAsync(CancellationToken cancellationToken = default);
}
