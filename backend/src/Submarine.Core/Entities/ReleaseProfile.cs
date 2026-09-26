namespace Submarine.Core.Entities;

/// <summary>
///     Required and ignored term restrictions applied to releases.
/// </summary>
public sealed class ReleaseProfile : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Whether the profile is active.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Required terms, substring or /regex/.</summary>
	public List<string> Required { get; set; } = [];

	/// <summary>Ignored terms, substring or /regex/.</summary>
	public List<string> Ignored { get; set; } = [];

	/// <summary>Restrict the profile to one indexer.</summary>
	public int? IndexerId { get; set; }

	/// <summary>Tags this profile applies to.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
