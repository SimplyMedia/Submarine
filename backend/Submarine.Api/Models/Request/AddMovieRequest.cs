namespace Submarine.Api.Models.Request;

public record AddMovieRequest
{
	public int TmdbId { get; set; }

	public string? Path { get; set; }

	public int? RootFolderId { get; set; }

	public int QualityProfileId { get; set; }

	public int LanguageProfileId { get; set; }

	public bool? IsAnime { get; set; }

	public bool Monitored { get; set; } = true;

	public List<string> Tags { get; set; } = new();
}
