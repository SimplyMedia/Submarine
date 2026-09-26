using System.Collections.Concurrent;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Tracks cancellation tokens of running commands so queued work can be cancelled from the API.
/// </summary>
public sealed class CommandExecutionRegistry
{
	private readonly ConcurrentDictionary<int, CancellationTokenSource> _running = new();

	/// <summary>
	///     Register the cancellation source of a running command.
	/// </summary>
	public void Register(int commandId, CancellationTokenSource cancellationTokenSource)
		=> _running[commandId] = cancellationTokenSource;

	/// <summary>
	///     Remove the registration of a finished command.
	/// </summary>
	public void Unregister(int commandId)
	{
		if (_running.TryRemove(commandId, out var cancellationTokenSource))
		{
			cancellationTokenSource.Dispose();
		}
	}

	/// <summary>
	///     Cancel a running command. Returns false when it is not running.
	/// </summary>
	public bool TryCancel(int commandId)
	{
		if (!_running.TryGetValue(commandId, out var cancellationTokenSource))
		{
			return false;
		}

		cancellationTokenSource.Cancel();
		return true;
	}
}
