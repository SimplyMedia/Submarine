using System.Collections.Generic;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.MediaFile.Naming;

/// <summary>
///     Inputs required to render a file name for a Movie
/// </summary>
public record MovieNamingContext
{
	/// <summary>
	///     The Title of the Movie
	/// </summary>
	public string MovieTitle { get; init; } = "";

	/// <summary>
	///     The Year of the Movie, if any
	/// </summary>
	public int? Year { get; init; }

	/// <summary>
	///     The Edition of the Movie, if any
	/// </summary>
	public string? Edition { get; init; }

	/// <summary>
	///     The Quality of the file, if any
	/// </summary>
	public QualityModel? QualityModel { get; init; }

	/// <summary>
	///     Languages included in the file, if any
	/// </summary>
	public IReadOnlyList<Language> Languages { get; init; } = new List<Language>();

	/// <summary>
	///     The Release Group of the file, if any
	/// </summary>
	public string? ReleaseGroup { get; init; }
}
