using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record UpdateMovieRequest
{
	public bool? Monitored { get; set; }

	public bool? IsAnime { get; set; }

	public MinimumAvailability? MinimumAvailability { get; set; }

	public List<string>? Tags { get; set; }
}
