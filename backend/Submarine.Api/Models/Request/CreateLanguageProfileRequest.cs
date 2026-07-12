using Submarine.Core.Languages;

namespace Submarine.Api.Models.Request;

public record CreateLanguageProfileRequest
{
	public string Name { get; set; } = null!;

	public List<Language> Languages { get; set; } = new();

	public Language Cutoff { get; set; }

	public bool UpgradeAllowed { get; set; }
}
