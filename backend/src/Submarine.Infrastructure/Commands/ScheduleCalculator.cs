namespace Submarine.Infrastructure.Commands;

/// <summary>
///     Pure next-run computation for scheduled tasks, derived from LastRun after restart.
/// </summary>
public static class ScheduleCalculator
{
	/// <summary>
	///     Compute the next run. A task that never ran is due immediately; otherwise the
	///     next run is the last run plus the interval.
	/// </summary>
	public static DateTime ComputeNextRun(DateTime? lastRun, int intervalMinutes, DateTime now)
		=> lastRun?.AddMinutes(intervalMinutes) ?? now;
}
