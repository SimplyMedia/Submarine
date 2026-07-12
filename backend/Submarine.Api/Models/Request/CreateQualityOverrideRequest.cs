using Submarine.Core.Quality;

namespace Submarine.Api.Models.Request;

public record CreateQualityOverrideRequest
{
	public string ReleaseGroup { get; set; } = null!;

	public QualitySource Source { get; set; }
}
