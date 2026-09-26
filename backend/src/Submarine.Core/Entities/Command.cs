using Submarine.Core.Commands;

namespace Submarine.Core.Entities;

/// <summary>
///     A persisted command queue row.
/// </summary>
public sealed class Command : Entity
{
	/// <summary>Registry name of the command type.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Command payload as JSON.</summary>
	public string Body { get; set; } = "{}";

	/// <summary>SHA256 hash of the body, used for dedupe.</summary>
	public string BodyHash { get; set; } = string.Empty;

	/// <summary>Lifecycle status.</summary>
	public CommandStatus Status { get; set; } = CommandStatus.QUEUED;

	/// <summary>Execution priority.</summary>
	public CommandPriority Priority { get; set; } = CommandPriority.NORMAL;

	/// <summary>What triggered the command.</summary>
	public CommandTrigger Trigger { get; set; }

	/// <summary>Progress percentage.</summary>
	public int Progress { get; set; }

	/// <summary>Progress or result message.</summary>
	public string? Message { get; set; }

	/// <summary>UTC timestamp execution started.</summary>
	public DateTime? StartedAt { get; set; }

	/// <summary>UTC timestamp execution ended.</summary>
	public DateTime? EndedAt { get; set; }

	/// <summary>Exception text when the command failed.</summary>
	public string? Exception { get; set; }
}
