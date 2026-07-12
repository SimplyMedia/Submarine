namespace Submarine.Api.Models.Request;

public record BatchMonitorEpisodesRequest
{
	public List<int> EpisodeIds { get; set; } = new();

	public bool Monitored { get; set; }
}
