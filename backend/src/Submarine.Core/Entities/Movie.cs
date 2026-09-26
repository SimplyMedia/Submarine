using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A movie in the library.
/// </summary>
public sealed class Movie : Entity
{
	/// <summary>TMDB id, unique.</summary>
	public int TmdbId { get; set; }

	/// <summary>IMDB id.</summary>
	public string? ImdbId { get; set; }

	/// <summary>Title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Sort friendly title.</summary>
	public string SortTitle { get; set; } = string.Empty;

	/// <summary>Lowercase cleaned title used for parsing and matching.</summary>
	public string CleanTitle { get; set; } = string.Empty;

	/// <summary>Original language title.</summary>
	public string? OriginalTitle { get; set; }

	/// <summary>Original language code.</summary>
	public string? OriginalLanguage { get; set; }

	/// <summary>TMDB keywords.</summary>
	public List<string> Keywords { get; set; } = [];

	/// <summary>Overview.</summary>
	public string? Overview { get; set; }

	/// <summary>Release year.</summary>
	public int? Year { get; set; }

	/// <summary>Runtime in minutes.</summary>
	public int? Runtime { get; set; }

	/// <summary>Studio.</summary>
	public string? Studio { get; set; }

	/// <summary>Poster image URL.</summary>
	public string? PosterUrl { get; set; }

	/// <summary>Backdrop image URL.</summary>
	public string? BackdropUrl { get; set; }

	/// <summary>Release status.</summary>
	public MovieStatus Status { get; set; } = MovieStatus.ANNOUNCED;

	/// <summary>Date the movie entered cinemas.</summary>
	public DateTime? InCinemasDate { get; set; }

	/// <summary>Digital release date.</summary>
	public DateTime? DigitalReleaseDate { get; set; }

	/// <summary>Physical release date.</summary>
	public DateTime? PhysicalReleaseDate { get; set; }

	/// <summary>Whether this is an anime movie.</summary>
	public bool IsAnime { get; set; }

	/// <summary>Whether the movie is monitored.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>Earliest availability before grabbing.</summary>
	public MinimumAvailability MinimumAvailability { get; set; } = MinimumAvailability.RELEASED;

	/// <summary>TMDB collection id.</summary>
	public int? TmdbCollectionId { get; set; }

	/// <summary>Title of the TMDB collection.</summary>
	public string? CollectionTitle { get; set; }

	/// <summary>Genres.</summary>
	public List<string> Genres { get; set; } = [];

	/// <summary>Content certification.</summary>
	public string? Certification { get; set; }

	/// <summary>YouTube trailer id.</summary>
	public string? YouTubeTrailerId { get; set; }

	/// <summary>UTC timestamp of the last automatic search.</summary>
	public DateTime? LastSearchTime { get; set; }

	/// <summary>UTC timestamp of the last metadata refresh.</summary>
	public DateTime? LastRefreshedAt { get; set; }

	/// <summary>Tags.</summary>
	public ICollection<Tag> Tags { get; set; } = [];

	/// <summary>Versions of this movie.</summary>
	public ICollection<MediaVersion> Versions { get; set; } = [];

	/// <summary>Files of this movie.</summary>
	public ICollection<MovieFile> Files { get; set; } = [];
}
