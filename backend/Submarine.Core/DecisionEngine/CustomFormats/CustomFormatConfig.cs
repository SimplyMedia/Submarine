using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.DecisionEngine.CustomFormats;

/// <summary>
///     Persisted configuration of a <see cref="CustomFormat" />
/// </summary>
public class CustomFormatConfig : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the custom format
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of the custom format
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	///     The Conditions of this custom format
	/// </summary>
	public List<CustomFormatCondition> Conditions { get; set; } = new();

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	///     Maps this configuration to its <see cref="CustomFormat" /> record
	/// </summary>
	public CustomFormat ToFormat()
		=> new(Id, Name, Conditions);
}
