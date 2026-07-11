using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Languages;

namespace Submarine.Core.Profile;

/// <summary>
///     A Language Profile defines which Languages are wanted for a Series or Movie
/// </summary>
public class LanguageProfile : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the language profile
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the language profile
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	///     Wanted Languages of this profile
	/// </summary>
	public List<Language> Languages { get; set; } = new();

	/// <summary>
	///     Language at which no further upgrade is wanted
	/// </summary>
	public Language Cutoff { get; set; }

	/// <summary>
	///     Whether Releases of this profile should be upgraded to a better Language once a lower Language was already
	///     downloaded
	/// </summary>
	public bool UpgradeAllowed { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
