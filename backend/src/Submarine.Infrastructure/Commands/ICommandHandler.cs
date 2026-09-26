using Submarine.Core.Commands;

namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Executes a command type. Handlers run inside a DI scope created per execution.
/// </summary>
public interface ICommandHandler<in TCommand>
	where TCommand : ICommand
{
	/// <summary>
	///     Execute the command.
	/// </summary>
	/// <param name="command">Deserialized command payload.</param>
	/// <param name="context">Execution context for progress reporting.</param>
	/// <param name="cancellationToken">Cancelled when the command is cancelled or the app shuts down.</param>
	Task ExecuteAsync(TCommand command, ICommandContext context, CancellationToken cancellationToken = default);
}
