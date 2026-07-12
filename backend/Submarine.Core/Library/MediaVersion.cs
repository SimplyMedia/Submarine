using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Library;

/// <summary>
///     A version of a <see cref="Series" /> or <see cref="Movie" /> kept in its own library folder,
///     e.g. a dedicated 4K copy alongside a 1080p one. Exactly one of
///     <see cref="SeriesId" /> or <see cref="MovieId" /> is set.
/// </summary>
public class MediaVersion : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the version
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Name of this version, e.g. "1080p" or "4K"
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	///     Id of the Series this version belongs to, if any
	/// </summary>
	public int? SeriesId { get; set; }

	/// <summary>
	///     Id of the Movie this version belongs to, if any
	/// </summary>
	public int? MovieId { get; set; }

	/// <summary>
	///     Id of the Quality Profile used for this version
	/// </summary>
	public int QualityProfileId { get; set; }

	/// <summary>
	///     Id of the Language Profile used for this version
	/// </summary>
	public int LanguageProfileId { get; set; }

	/// <summary>
	///     Full folder path this version's files are stored under
	/// </summary>
	public string Path { get; set; } = string.Empty;

	/// <summary>
	///     Whether this version is monitored for new files
	/// </summary>
	public bool Monitored { get; set; } = true;

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
