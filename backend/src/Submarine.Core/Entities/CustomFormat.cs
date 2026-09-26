namespace Submarine.Core.Entities;

/// <summary>
///     A custom format scoring releases, made up of specifications.
/// </summary>
public sealed class CustomFormat : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Include this format in the file name when renaming.</summary>
	public bool IncludeCustomFormatWhenRenaming { get; set; }

	/// <summary>Specifications all or some of which must match, depending on Required flags.</summary>
	public List<CustomFormatSpecification> Specifications { get; set; } = [];
}
