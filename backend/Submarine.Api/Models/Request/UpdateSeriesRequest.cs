using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record UpdateSeriesRequest
{
	public bool? Monitored { get; set; }

	public string? Path { get; set; }

	public int? QualityProfileId { get; set; }

	public int? LanguageProfileId { get; set; }

	public SeriesType? Type { get; set; }

	public List<string>? Tags { get; set; }
}
