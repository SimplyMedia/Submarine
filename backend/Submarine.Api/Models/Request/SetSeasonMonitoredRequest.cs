namespace Submarine.Api.Models.Request;

public record SetSeasonMonitoredRequest
{
	public bool Monitored { get; set; }
}
