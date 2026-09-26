namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Execution context handed to command handlers.
/// </summary>
public interface ICommandContext
{
	/// <summary>
	///     Id of the persisted Command row.
	/// </summary>
	int CommandId { get; }

	/// <summary>
	///     Report progress, persisted on the Command row and broadcast as a CommandUpdated event.
	/// </summary>
	/// <param name="percent">Progress percentage, clamped to 0-100.</param>
	/// <param name="message">Optional message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task ReportProgressAsync(int percent, string? message = null, CancellationToken cancellationToken = default);
}
