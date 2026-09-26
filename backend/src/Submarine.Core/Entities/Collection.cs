using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     A monitored TMDB collection.
/// </summary>
public sealed class Collection : Entity
{
	/// <summary>TMDB collection id, unique.</summary>
	public int TmdbCollectionId { get; set; }

	/// <summary>Title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Overview.</summary>
	public string? Overview { get; set; }

	/// <summary>Poster image URL.</summary>
	public string? PosterUrl { get; set; }

	/// <summary>Whether the collection is monitored for new movies.</summary>
	public bool Monitored { get; set; } = true;

	/// <summary>Root folder new movies are added to.</summary>
	public int? RootFolderId { get; set; }

	/// <summary>Quality profile for new movies.</summary>
	public int? QualityProfileId { get; set; }

	/// <summary>Language profile for new movies.</summary>
	public int? LanguageProfileId { get; set; }

	/// <summary>Earliest availability before grabbing.</summary>
	public MinimumAvailability MinimumAvailability { get; set; } = MinimumAvailability.RELEASED;

	/// <summary>Search for movies on add.</summary>
	public bool SearchOnAdd { get; set; }
}
