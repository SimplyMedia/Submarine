using Submarine.Core.Profile;

namespace Submarine.Api.Models.Request;

public record CreateQualityProfileRequest
{
	public string Name { get; set; } = null!;

	public bool UpgradeAllowed { get; set; }

	public int Cutoff { get; set; }

	public List<QualityProfileItem> Items { get; set; } = new();

	public Dictionary<int, int> FormatScores { get; set; } = new();
}
