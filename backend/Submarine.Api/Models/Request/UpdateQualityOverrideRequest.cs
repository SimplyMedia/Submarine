using Submarine.Core.Quality;

namespace Submarine.Api.Models.Request;

public record UpdateQualityOverrideRequest
{
	public string ReleaseGroup { get; set; } = null!;

	public QualitySource Source { get; set; }
}
