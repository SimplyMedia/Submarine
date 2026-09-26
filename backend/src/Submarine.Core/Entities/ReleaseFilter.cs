using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     Global allow, block or prefer rule for releases.
/// </summary>
public sealed class ReleaseFilter : Entity
{
	/// <summary>Field the values match against.</summary>
	public ReleaseFilterField Field { get; set; }

	/// <summary>Values to match.</summary>
	public List<string> Values { get; set; } = [];

	/// <summary>How matches are treated.</summary>
	public ReleaseFilterMode Mode { get; set; }

	/// <summary>Tier for prefer filters, lower tiers win.</summary>
	public int Tier { get; set; }
}
