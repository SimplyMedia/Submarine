namespace Submarine.Core.Entities;

/// <summary>
///     A tag attachable to many entities through skip navigations.
/// </summary>
public sealed class Tag : Entity
{
	/// <summary>Unique label.</summary>
	public string Label { get; set; } = string.Empty;
}
