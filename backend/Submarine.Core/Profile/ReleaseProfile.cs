using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Profile;

/// <summary>
///     A Release Profile restricts which Releases are grabbed based on terms in their title
/// </summary>
public class ReleaseProfile : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the release profile
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the release profile
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Whether this profile is applied
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	///     Terms a Release's title must all contain to be grabbed, matched case-insensitively as a substring or, when
	///     wrapped in /.../, as a regular expression
	/// </summary>
	public List<string> Required { get; set; } = new();

	/// <summary>
	///     Terms a Release's title is rejected for containing any of, matched case-insensitively as a substring or,
	///     when wrapped in /.../, as a regular expression
	/// </summary>
	public List<string> Ignored { get; set; } = new();

	/// <summary>
	///     Indexer this profile is restricted to, null applies to all indexers
	/// </summary>
	public string? Indexer { get; set; }

	/// <summary>
	///     Tags a Series or Movie must have for this profile to apply
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
