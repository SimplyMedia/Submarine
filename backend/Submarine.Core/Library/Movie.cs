using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Submarine.Core.Database;

namespace Submarine.Core.Library;

/// <summary>
///     A Movie which is tracked and monitored for download
/// </summary>
public class Movie : ICreatable, IUpdatable
{
	/// <summary>
	///     Id of the movie
	/// </summary>
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	///     TheMovieDB Id of this movie, must be unique
	/// </summary>
	public int TmdbId { get; set; }

	/// <summary>
	///     IMDB Id of this movie
	/// </summary>
	public string? ImdbId { get; set; }

	/// <summary>
	///     Title of the movie
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	///     Title used for sorting this movie
	/// </summary>
	public string? SortTitle { get; set; }

	/// <summary>
	///     Overview of the movie
	/// </summary>
	public string? Overview { get; set; }

	/// <summary>
	///     Year this movie was released
	/// </summary>
	public int? Year { get; set; }

	/// <summary>
	///     Runtime of the movie in minutes
	/// </summary>
	public int? Runtime { get; set; }

	/// <summary>
	///     Studio which produced this movie
	/// </summary>
	public string? Studio { get; set; }

	/// <summary>
	///     Release date of this movie
	/// </summary>
	public DateTimeOffset? ReleaseDate { get; set; }

	/// <summary>
	///     Whether this movie is an Anime movie
	/// </summary>
	public bool IsAnime { get; set; }

	/// <summary>
	///     Path on disk this movie is stored at
	/// </summary>
	public string Path { get; set; }

	/// <summary>
	///     Whether this movie is monitored for download
	/// </summary>
	public bool Monitored { get; set; }

	/// <summary>
	///     Id of the Quality Profile used for this movie
	/// </summary>
	public int QualityProfileId { get; set; }

	/// <summary>
	///     Id of the Language Profile used for this movie
	/// </summary>
	public int LanguageProfileId { get; set; }

	/// <summary>
	///     Tags of this movie
	/// </summary>
	public List<string> Tags { get; set; } = new();

	/// <summary>
	///     Id of the Movie File which satisfies this movie, if any
	/// </summary>
	public int? MovieFileId { get; set; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; set; }

	/// <inheritdoc />
	public DateTimeOffset UpdatedAt { get; set; }
}
