using Submarine.Core.Commands;

namespace Submarine.Core.Events;

/// <summary>
///     Published whenever the state of a command row changes.
/// </summary>
/// <param name="CommandId">Id of the command row.</param>
/// <param name="Name">Command name.</param>
/// <param name="Status">New status.</param>
/// <param name="Progress">Progress percentage.</param>
/// <param name="Message">Optional progress message.</param>
/// <param name="StartedAt">When execution started, if it did.</param>
/// <param name="EndedAt">When execution ended, if it did.</param>
public sealed record CommandUpdated(
	int CommandId,
	string Name,
	CommandStatus Status,
	int Progress,
	string? Message,
	DateTime? StartedAt,
	DateTime? EndedAt) : IDomainEvent;
