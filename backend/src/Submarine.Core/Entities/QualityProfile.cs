namespace Submarine.Core.Entities;

/// <summary>
///     Ordered quality choices with custom format scores. Items are ordered low to high quality.
/// </summary>
public sealed class QualityProfile : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Whether upgrades beyond the cutoff are allowed.</summary>
	public bool UpgradeAllowed { get; set; } = true;

	/// <summary>Index into Items of the cutoff quality.</summary>
	public int Cutoff { get; set; }

	/// <summary>Quality items, ordered low to high.</summary>
	public List<QualityProfileItem> Items { get; set; } = [];

	/// <summary>Custom format scores.</summary>
	public List<ProfileFormatItem> FormatItems { get; set; } = [];

	/// <summary>Minimum total custom format score to grab.</summary>
	public int MinFormatScore { get; set; }

	/// <summary>Total custom format score required to upgrade past the cutoff.</summary>
	public int CutoffFormatScore { get; set; }

	/// <summary>Minimum custom format score for further upgrades.</summary>
	public int MinUpgradeFormatScore { get; set; }
}
