using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

public record MovieEditorRequest
{
	public List<int> MovieIds { get; set; } = new();

	public bool? Monitored { get; set; }

	public int? QualityProfileId { get; set; }

	public int? LanguageProfileId { get; set; }

	public MinimumAvailability? MinimumAvailability { get; set; }

	public List<string>? AddTags { get; set; }

	public List<string>? RemoveTags { get; set; }
}
