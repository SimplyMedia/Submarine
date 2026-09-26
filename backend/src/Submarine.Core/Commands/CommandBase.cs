namespace Submarine.Core.Commands;

/// <summary>
///     Base record for commands. Concrete commands are records so their payload
///     can be serialized to JSON and deserialized by the command registry.
/// </summary>
public abstract record CommandBase : ICommand
{
}
