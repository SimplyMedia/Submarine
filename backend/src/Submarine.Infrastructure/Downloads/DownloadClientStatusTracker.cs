using System.Collections.Concurrent;
using Submarine.Core.Events;

namespace Submarine.Infrastructure.Downloads;

/// <summary>
///     In-memory escalating backoff tracker for download client failures.
/// </summary>
public sealed class DownloadClientStatusTracker(IEventBus eventBus, TimeProvider timeProvider) : IDownloadClientStatusTracker
{
	private static readonly int[] BackoffMinutes = [5, 15, 30, 60, 180, 360, 720, 1440];

	private readonly ConcurrentDictionary<int, State> _states = new();

	/// <inheritdoc />
	public bool IsAvailable(int clientId)
		=> !_states.TryGetValue(clientId, out var state)
		   || state.DisabledUntil is not { } until
		   || timeProvider.GetUtcNow().UtcDateTime >= until;

	/// <inheritdoc />
	public async Task RecordSuccessAsync(int clientId, CancellationToken cancellationToken = default)
	{
		if (_states.TryRemove(clientId, out var previous) && previous.DisabledUntil is not null)
		{
			await eventBus.PublishAsync(new DownloadClientStatusChangedEvent(clientId), cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(int clientId, string message, CancellationToken cancellationToken = default)
	{
		var wasAvailable = IsAvailable(clientId);
		var state = _states.GetOrAdd(clientId, static _ => new State());
		var level = Math.Min(state.EscalationLevel, BackoffMinutes.Length - 1);
		state.DisabledUntil = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(BackoffMinutes[level]);
		state.EscalationLevel++;
		state.LastMessage = message;

		if (wasAvailable)
		{
			await eventBus.PublishAsync(new DownloadClientStatusChangedEvent(clientId), cancellationToken);
		}
	}

	private sealed class State
	{
		public int EscalationLevel;
		public DateTime? DisabledUntil;
		public string? LastMessage;
	}
}
