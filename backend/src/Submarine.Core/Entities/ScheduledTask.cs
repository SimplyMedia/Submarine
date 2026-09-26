namespace Submarine.Core.Entities;

/// <summary>
///     A recurring command, executed by the scheduler.
/// </summary>
public sealed class ScheduledTask : Entity
{
	/// <summary>Command name, unique.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Interval between runs in minutes.</summary>
	public int IntervalMinutes { get; set; }

	/// <summary>UTC timestamp of the last run.</summary>
	public DateTime? LastRun { get; set; }

	/// <summary>UTC timestamp of the next scheduled run, computed from LastRun after restart.</summary>
	public DateTime? NextRun { get; set; }
}
