namespace Submarine.Core.ImportList;

/// <summary>
///     The source an <see cref="ImportList" /> fetches its items from
/// </summary>
public enum ImportListType
{
	/// <summary>
	///     A user created TheMovieDB list
	/// </summary>
	TMDB_LIST,

	/// <summary>
	///     TheMovieDB popular chart
	/// </summary>
	TMDB_POPULAR,

	/// <summary>
	///     A user created Trakt list
	/// </summary>
	TRAKT_LIST,

	/// <summary>
	///     The current anime season on AniList
	/// </summary>
	ANILIST_SEASON
}
