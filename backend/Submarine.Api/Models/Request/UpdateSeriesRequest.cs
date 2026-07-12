using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record UpdateSeriesRequest
{
	public bool? Monitored { get; set; }

	public SeriesType? Type { get; set; }

	public List<string>? Tags { get; set; }
}
