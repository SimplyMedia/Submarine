namespace Submarine.Core.Entities;

/// <summary>
///     Base class for all persisted entities. Timestamps are UTC and maintained by the DbContext.
/// </summary>
public abstract class Entity
{
	/// <summary>Database identity.</summary>
	public int Id { get; set; }

	/// <summary>UTC timestamp of creation.</summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>UTC timestamp of the last update.</summary>
	public DateTime UpdatedAt { get; set; }
}
