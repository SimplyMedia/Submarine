namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Tracks indexer request failures and applies an escalating backoff before an indexer is queried again.
/// </summary>
public interface IIndexerStatusService
{
	/// <summary>Whether the indexer is not currently disabled by backoff.</summary>
	Task<bool> IsAvailableAsync(int indexerId, CancellationToken cancellationToken = default);

	/// <summary>Records a successful request, clearing any backoff.</summary>
	Task RecordSuccessAsync(int indexerId, CancellationToken cancellationToken = default);

	/// <summary>Records a failed request, escalating the backoff window.</summary>
	Task RecordFailureAsync(int indexerId, CancellationToken cancellationToken = default);
}
