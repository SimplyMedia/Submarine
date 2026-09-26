namespace Submarine.Core.Enums;

/// <summary>
///     Type of an import list.
/// </summary>
public enum ImportListType
{
	/// <summary>TMDB list.</summary>
	TMDB_LIST,

	/// <summary>TMDB popular.</summary>
	TMDB_POPULAR,

	/// <summary>TMDB collection.</summary>
	TMDB_COLLECTION,

	/// <summary>TMDB person.</summary>
	TMDB_PERSON,

	/// <summary>Trakt list.</summary>
	TRAKT_LIST,

	/// <summary>Trakt popular.</summary>
	TRAKT_POPULAR,

	/// <summary>Trakt user.</summary>
	TRAKT_USER,

	/// <summary>AniList season.</summary>
	ANILIST_SEASON,

	/// <summary>Plex watchlist.</summary>
	PLEX,

	/// <summary>Another Sonarr instance.</summary>
	SONARR,

	/// <summary>Another Radarr instance.</summary>
	RADARR,

	/// <summary>StevenLu list.</summary>
	STEVEN_LU,

	/// <summary>Custom list.</summary>
	CUSTOM
}
