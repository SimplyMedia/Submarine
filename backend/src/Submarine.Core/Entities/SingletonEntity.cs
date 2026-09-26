namespace Submarine.Core.Entities;

/// <summary>
///     Base class for singleton configuration rows. Always stored with Id 1, no CreatedAt.
/// </summary>
public abstract class SingletonEntity
{
	/// <summary>Fixed identity, always 1.</summary>
	public int Id { get; set; } = 1;

	/// <summary>UTC timestamp of the last update.</summary>
	public DateTime UpdatedAt { get; set; }
}
