using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     Persisted configuration of a <see cref="ReleaseFilter" />
/// </summary>
public class ReleaseFilterConfig : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the filter
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     The Field of the Release this Filter matches against
	/// </summary>
	public FilterField Field { get; set; }

	/// <summary>
	///     The values matched case-insensitively against the rendered Field
	/// </summary>
	public List<string> Values { get; set; } = new();

	/// <summary>
	///     The Mode deciding how a match affects the Release
	/// </summary>
	public FilterMode Mode { get; set; }

	/// <summary>
	///     The Tier of this Filter, only meaningful for <see cref="FilterMode.PREFER" />; lower is more preferred
	/// </summary>
	public int Tier { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	///     Maps this configuration to its <see cref="ReleaseFilter" /> record
	/// </summary>
	public ReleaseFilter ToFilter()
		=> new(Id, Field, Values, Mode, Tier);
}
