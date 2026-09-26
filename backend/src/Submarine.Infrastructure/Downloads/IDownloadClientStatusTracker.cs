namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     Tracks in-memory download client failures with escalating backoff, so the queue monitor stops
///     hammering an unreachable client.
/// </summary>
public interface IDownloadClientStatusTracker
{
	/// <summary>
	///     Whether the client should currently be contacted.
	/// </summary>
	bool IsAvailable(int clientId);

	/// <summary>
	///     Record a successful contact, clearing any backoff.
	/// </summary>
	Task RecordSuccessAsync(int clientId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Record a failed contact, escalating the backoff.
	/// </summary>
	Task RecordFailureAsync(int clientId, string message, CancellationToken cancellationToken = default);
}
