using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFile;

/// <summary>
///     A File on disk which satisfies a Movie
/// </summary>
public class MovieFile : ICreatable
{
	/// <summary>
	///     Id of the movie file
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     Id of the movie this file belongs to
	/// </summary>
	public int MovieId { get; set; }

	/// <summary>
	///     Id of the version this file belongs to
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
	///     Edition of this movie file, e.g. Director's Cut
	/// </summary>
	public string? Edition { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }
}
