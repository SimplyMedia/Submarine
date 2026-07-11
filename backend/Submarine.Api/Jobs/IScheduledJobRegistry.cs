namespace Submarine.Api.Jobs;

public interface IScheduledJobRegistry
{
	IReadOnlyCollection<ScheduledJobStatus> GetAll();

	ScheduledJobStatus? Get(string name);
}
