namespace Submarine.Api.Models.Request;

public record UpdateMovieRequest
{
	public bool? Monitored { get; set; }

	public string? Path { get; set; }

	public int? QualityProfileId { get; set; }

	public int? LanguageProfileId { get; set; }

	public bool? IsAnime { get; set; }

	public List<string>? Tags { get; set; }
}
