namespace Submarine.Core.Entities;

/// <summary>
///     A rule that automatically applies or removes tags on series and movies matching its specifications.
/// </summary>
public sealed class AutoTaggingRule : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Whether the rule is enabled.</summary>
	public bool Enable { get; set; } = true;

	/// <summary>Remove the tags automatically when the item no longer matches.</summary>
	public bool RemoveTagsAutomatically { get; set; }

	/// <summary>Specifications all or some of which must match, depending on Required flags.</summary>
	public List<AutoTaggingSpecification> Specifications { get; set; } = [];

	/// <summary>Tags applied when the rule matches.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
