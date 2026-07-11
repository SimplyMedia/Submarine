using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Tag;

/// <summary>
///     A Tag which can be attached to other entities for grouping and filtering purposes
/// </summary>
public class Tag : ICreatable
{
	/// <summary>
	///     Id of the tag
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Label of the tag, must be unique
	/// </summary>
	public string Label { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }
}
