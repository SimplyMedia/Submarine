namespace Submarine.Core.Entities;

/// <summary>
///     One "want" of a series or movie: its own profiles, path and monitored flag.
///     A version belongs to either a series or a movie.
/// </summary>
public sealed class MediaVersion : Entity
{
	/// <summary>Display name, for example 1080p.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Id of the series this version belongs to, if any.</summary>
	public int? SeriesId { get; set; }

	/// <summary>Id of the movie this version belongs to, if any.</summary>
	public int? MovieId { get; set; }

	/// <summary>Quality profile used for decisions.</summary>
	public int QualityProfileId { get; set; }

	/// <summary>Language profile used for decisions.</summary>
	public int LanguageProfileId { get; set; }

	/// <summary>Root folder files are imported into.</summary>
	public int RootFolderId { get; set; }

	/// <summary>Path of the version folder inside the root folder.</summary>
	public string Path { get; set; } = string.Empty;

	/// <summary>Whether this version is monitored.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>Series this version belongs to, if any.</summary>
	public Series? Series { get; set; }

	/// <summary>Movie this version belongs to, if any.</summary>
	public Movie? Movie { get; set; }

	/// <summary>Episode files of this version.</summary>
	public ICollection<EpisodeFile> EpisodeFiles { get; set; } = [];

	/// <summary>Movie files of this version.</summary>
	public ICollection<MovieFile> MovieFiles { get; set; } = [];
}
