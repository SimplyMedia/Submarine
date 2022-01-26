using System;

namespace Submarine.Core.Database;

/// <summary>
/// Interface which abstracts UpdatedAt for Database entities
/// </summary>
public interface IUpdatable
{
	/// <summary>
	/// When this entity was last updated in the Database
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}
