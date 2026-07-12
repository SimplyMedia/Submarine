using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFile;

/// <summary>
///     A File on disk which satisfies an Episode of a Series
/// </summary>
public class EpisodeFile : ICreatable
{
	/// <summary>
	///     Id of the episode file
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Id of the series this episode file belongs to
	/// </summary>
	public int SeriesId { get; set; }

	/// <summary>
	///     Id of the version this episode file belongs to
	/// </summary>
	public int MediaVersionId { get; set; }

	/// <summary>
	///     Path of this file, relative to its version's path
	/// </summary>
	public string RelativePath { get; set; }

	/// <summary>
	///     Size of this file in bytes
	/// </summary>
	public long Size { get; set; }

	/// <summary>
	///     Date this file was added
	/// </summary>
	public DateTimeOffset DateAdded { get; set; }

	/// <summary>
	///     Quality of this file
	/// </summary>
	public QualityModel Quality { get; set; }

	/// <summary>
	///     Languages of this file
	/// </summary>
	public List<Language> Languages { get; set; } = new();

	/// <summary>
	///     Release group which released this file
	/// </summary>
	public string? ReleaseGroup { get; set; }

	/// <summary>
	///     Whether this file was named from a placeholder name instead of a parsed Release name
	/// </summary>
	public bool NamedFromPlaceholder { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	///     Episodes this file satisfies
	/// </summary>
	public ICollection<Library.Episode> Episodes { get; set; } = new List<Library.Episode>();
}
