namespace Submarine.Core.Indexers;

/// <summary>
///     Supported search parameters of an indexer search mode
/// </summary>
[Flags]
public enum SearchParams
{
	/// <summary>
	///     No parameters supported
	/// </summary>
	None = 0,

	/// <summary>
	///     Free text query
	/// </summary>
	Q = 1,

	/// <summary>
	///     Season number
	/// </summary>
	Season = 1 << 1,

	/// <summary>
	///     Episode number
	/// </summary>
	Ep = 1 << 2,

	/// <summary>
	///     IMDb id
	/// </summary>
	ImdbId = 1 << 3,

	/// <summary>
	///     TMDB id
	/// </summary>
	TmdbId = 1 << 4,

	/// <summary>
	///     TVDB id
	/// </summary>
	TvdbId = 1 << 5,

	/// <summary>
	///     TVRage id
	/// </summary>
	Rid = 1 << 6,

	/// <summary>
	///     Release year
	/// </summary>
	Year = 1 << 7,

	/// <summary>
	///     Genre (music, book)
	/// </summary>
	Genre = 1 << 8,

	/// <summary>
	///     Artist name (music)
	/// </summary>
	Artist = 1 << 9,

	/// <summary>
	///     Album name (music)
	/// </summary>
	Album = 1 << 10,

	/// <summary>
	///     Record label (music)
	/// </summary>
	Label = 1 << 11,

	/// <summary>
	///     Track name (music)
	/// </summary>
	Track = 1 << 12,

	/// <summary>
	///     Author name (book)
	/// </summary>
	Author = 1 << 13,

	/// <summary>
	///     Book title (book)
	/// </summary>
	Title = 1 << 14,

	/// <summary>
	///     Publisher (book)
	/// </summary>
	Publisher = 1 << 15
}
