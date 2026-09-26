using Submarine.Core.Commands;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Persisted command queue. Enqueue is deduplicated: at most one queued or running
///     row may exist per name and body hash.
/// </summary>
public interface ICommandQueue
{
	/// <summary>
	///     Persist the command and signal the executor. Returns the created row, or the
	///     existing row when an identical command is already queued or running.
	/// </summary>
	Task<Command> EnqueueAsync(
		ICommand command,
		CommandTrigger trigger,
		CommandPriority priority = CommandPriority.NORMAL,
		CancellationToken cancellationToken = default);
}
