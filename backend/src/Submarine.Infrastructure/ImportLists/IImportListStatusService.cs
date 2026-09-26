namespace Submarine.Infrastructure.ImportLists;

/// <summary>
///     Tracks import list fetch results: last successful sync time, for minimum refresh interval
///     enforcement, and an escalating backoff after repeated failures.
/// </summary>
public interface IImportListStatusService
{
	/// <summary>Whether the list is not currently disabled by backoff.</summary>
	Task<bool> IsAvailableAsync(int importListId, CancellationToken cancellationToken = default);

	/// <summary>UTC timestamp of the last successful fetch, null when never synced.</summary>
	Task<DateTime?> GetLastSyncAsync(int importListId, CancellationToken cancellationToken = default);

	/// <summary>Records a successful fetch, clearing any backoff and updating the last sync time.</summary>
	Task RecordSuccessAsync(int importListId, CancellationToken cancellationToken = default);

	/// <summary>Records a failed fetch, escalating the backoff window.</summary>
	Task RecordFailureAsync(int importListId, CancellationToken cancellationToken = default);
}
