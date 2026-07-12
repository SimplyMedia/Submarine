namespace Submarine.Api.Models.Request;

public record SeriesEditorRequest
{
	public List<int> SeriesIds { get; set; } = new();

	public bool? Monitored { get; set; }

	public int? QualityProfileId { get; set; }

	public int? LanguageProfileId { get; set; }

	public List<string>? AddTags { get; set; }

	public List<string>? RemoveTags { get; set; }
}
