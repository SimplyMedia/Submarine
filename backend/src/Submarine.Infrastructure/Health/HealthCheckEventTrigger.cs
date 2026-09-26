using Submarine.Core.Commands;
using Submarine.Core.Events;
using Submarine.Infrastructure.Commands;

namespace Submarine.Infrastructure.Health;

/// <summary>
///     Re-runs the health checks shortly after events that can change their outcome: indexer or
///     download client status changes, and series or movie removal. The command queue dedupes
///     repeated enqueues, so bursts of events only trigger one extra run.
/// </summary>
public sealed class HealthCheckEventTrigger(ICommandQueue queue) :
	IEventHandler<IndexerStatusChangedEvent>,
	IEventHandler<DownloadClientStatusChangedEvent>,
	IEventHandler<SeriesDeletedEvent>,
	IEventHandler<MovieDeletedEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(IndexerStatusChangedEvent @event, CancellationToken cancellationToken = default)
		=> EnqueueAsync(cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(DownloadClientStatusChangedEvent @event, CancellationToken cancellationToken = default)
		=> EnqueueAsync(cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(SeriesDeletedEvent @event, CancellationToken cancellationToken = default)
		=> EnqueueAsync(cancellationToken);

	/// <inheritdoc />
	public Task HandleAsync(MovieDeletedEvent @event, CancellationToken cancellationToken = default)
		=> EnqueueAsync(cancellationToken);

	private Task EnqueueAsync(CancellationToken cancellationToken)
		=> queue.EnqueueAsync(new HealthCheckCommand(), CommandTrigger.SYSTEM, cancellationToken: cancellationToken);
}
