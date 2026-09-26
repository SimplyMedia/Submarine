using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     File and folder naming configuration, singleton row with Id 1.
/// </summary>
public sealed class NamingConfig : SingletonEntity
{
	/// <summary>Rename episode files on import.</summary>
	public bool RenameEpisodes { get; set; } = true;

	/// <summary>Rename movie files on import.</summary>
	public bool RenameMovies { get; set; } = true;

	/// <summary>Replace characters that are illegal on the target file system.</summary>
	public bool ReplaceIllegalCharacters { get; set; } = true;

	/// <summary>How colons are replaced in names.</summary>
	public ColonReplacement ColonReplacement { get; set; } = ColonReplacement.SMART;

	/// <summary>Standard episode file name format.</summary>
	public string StandardEpisodeFormat { get; set; } = "{Series Title} - S{Season:00}E{Episode:00} - {Episode Title}";

	/// <summary>Daily episode file name format.</summary>
	public string DailyEpisodeFormat { get; set; } = "{Series Title} - {Air-Date} - {Episode Title}";

	/// <summary>Anime episode file name format.</summary>
	public string AnimeEpisodeFormat { get; set; } = "{Series Title} - {Absolute:000} - {Episode Title}";

	/// <summary>Series folder format.</summary>
	public string SeriesFolderFormat { get; set; } = "{Series Title}";

	/// <summary>Season folder format.</summary>
	public string SeasonFolderFormat { get; set; } = "Season {Season:00}";

	/// <summary>Specials folder format.</summary>
	public string SpecialsFolderFormat { get; set; } = "Specials";

	/// <summary>Movie file name format.</summary>
	public string MovieFormat { get; set; } = "{Movie Title} ({Year})";

	/// <summary>Movie folder format.</summary>
	public string MovieFolderFormat { get; set; } = "{Movie Title} ({Year})";

	/// <summary>How multi-episode files are named.</summary>
	public MultiEpisodeStyle MultiEpisodeStyle { get; set; } = MultiEpisodeStyle.PREFIXED_RANGE;
}
