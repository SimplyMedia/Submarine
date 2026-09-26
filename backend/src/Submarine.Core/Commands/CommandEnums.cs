namespace Submarine.Core.Commands;

/// <summary>
///     Lifecycle status of a queued command.
/// </summary>
public enum CommandStatus
{
	/// <summary>Waiting for an executor slot.</summary>
	QUEUED,

	/// <summary>Currently executing.</summary>
	RUNNING,

	/// <summary>Finished successfully.</summary>
	COMPLETED,

	/// <summary>Finished with an error.</summary>
	FAILED,

	/// <summary>Cancelled before or during execution.</summary>
	CANCELLED
}

/// <summary>
///     Execution priority of a command. Higher priority commands are claimed first.
/// </summary>
public enum CommandPriority
{
	/// <summary>Background work.</summary>
	LOW,

	/// <summary>Default priority.</summary>
	NORMAL,

	/// <summary>User initiated work that should run first.</summary>
	HIGH
}

/// <summary>
///     What caused a command to be enqueued.
/// </summary>
public enum CommandTrigger
{
	/// <summary>Triggered through the API by a user.</summary>
	MANUAL,

	/// <summary>Triggered by the scheduler.</summary>
	SCHEDULED,

	/// <summary>Triggered internally by the application.</summary>
	SYSTEM
}
