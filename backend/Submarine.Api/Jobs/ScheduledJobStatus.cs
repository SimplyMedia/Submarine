namespace Submarine.Api.Jobs;

public sealed record ScheduledJobStatus(string Name, TimeSpan Interval, DateTimeOffset? LastRun, DateTimeOffset? NextRun,
	bool Running);
