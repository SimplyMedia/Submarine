using System.Collections.Concurrent;

namespace Submarine.Api.Jobs;

public sealed class ScheduledJobRegistry : IScheduledJobRegistry
{
	private sealed class Entry
	{
		public required TimeSpan Interval { get; init; }
		public DateTimeOffset? LastRun { get; set; }
		public DateTimeOffset? NextRun { get; set; }
		public bool Running { get; set; }
	}

	private readonly ConcurrentDictionary<string, Entry> _entries = new();

	internal void Register(string name, TimeSpan interval, DateTimeOffset nextRun)
		=> _entries[name] = new Entry { Interval = interval, NextRun = nextRun };

	internal bool TryMarkRunning(string name)
	{
		var entry = _entries[name];

		lock (entry)
		{
			if (entry.Running)
				return false;

			entry.Running = true;
			return true;
		}
	}

	internal void MarkCompleted(string name, DateTimeOffset lastRun, DateTimeOffset nextRun)
	{
		var entry = _entries[name];

		lock (entry)
		{
			entry.LastRun = lastRun;
			entry.NextRun = nextRun;
			entry.Running = false;
		}
	}

	public IReadOnlyCollection<ScheduledJobStatus> GetAll()
		=> _entries.Select(kv => ToStatus(kv.Key, kv.Value)).ToList();

	public ScheduledJobStatus? Get(string name)
		=> _entries.TryGetValue(name, out var entry) ? ToStatus(name, entry) : null;

	private static ScheduledJobStatus ToStatus(string name, Entry entry)
	{
		lock (entry)
			return new ScheduledJobStatus(name, entry.Interval, entry.LastRun, entry.NextRun, entry.Running);
	}
}
