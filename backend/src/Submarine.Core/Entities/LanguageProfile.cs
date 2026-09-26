using Submarine.Core.Languages;

namespace Submarine.Core.Entities;

/// <summary>
///     Wanted languages and their cutoff.
/// </summary>
public sealed class LanguageProfile : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Allowed languages.</summary>
	public List<Language> Languages { get; set; } = [];

	/// <summary>Cutoff language, upgrades stop once reached.</summary>
	public Language Cutoff { get; set; }

	/// <summary>Whether upgrades beyond the cutoff are allowed.</summary>
	public bool UpgradeAllowed { get; set; } = true;
}
