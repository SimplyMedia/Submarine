using Submarine.Core.Commands;
using Submarine.Core.Entities;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Picks the next command to execute: highest priority first, then oldest first,
///     never a command whose name is already running.
/// </summary>
public static class CommandSelector
{
	/// <summary>
	///     Select the next claimable command, or null when nothing is claimable.
	/// </summary>
	/// <param name="queued">Queued candidates in arbitrary order.</param>
	/// <param name="runningNames">Names currently executing.</param>
	public static Command? SelectNext(IEnumerable<Command> queued, IReadOnlySet<string> runningNames)
		=> queued
			.Where(x => x.Status == CommandStatus.QUEUED && !runningNames.Contains(x.Name))
			.OrderByDescending(x => x.Priority)
			.ThenBy(x => x.Id)
			.FirstOrDefault();
}
