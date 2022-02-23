using System;

namespace Submarine.Core.Database;

/// <summary>
/// Interface which abstracts CreatedAt for Database entities
/// </summary>
public interface ICreatable
{
	/// <summary>
	/// When this entity was created in the database
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }
}
