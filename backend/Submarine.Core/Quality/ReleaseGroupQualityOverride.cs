using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Quality;

/// <summary>
///     User-defined default Quality Source for a release group whose releases do not state a quality
/// </summary>
public class ReleaseGroupQualityOverride : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the override
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Release group this override applies to, unique (case-insensitive)
	/// </summary>
	public string ReleaseGroup { get; set; } = string.Empty;

	/// <summary>
	///     Quality Source to assume for releases of this group without an explicit quality
	/// </summary>
	public QualitySource Source { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
