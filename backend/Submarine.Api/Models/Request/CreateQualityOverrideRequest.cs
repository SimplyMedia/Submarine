using Submarine.Core.Quality;

namespace Submarine.Api.Models.Request;

public record CreateQualityOverrideRequest
{
	public string ReleaseGroup { get; set; }

	public QualitySource Source { get; set; }
}
