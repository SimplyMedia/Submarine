using Submarine.Core.Library;

namespace Submarine.Api.Models.Request;

/// <summary>
///     Request to adopt existing library folders as Series or Movies, registering their files in place
/// </summary>
public record LibraryImportRequest
{
	/// <summary>
	///     Folders to import
	/// </summary>
	public List<LibraryImportItem> Items { get; set; } = new();

	/// <summary>
	///     Kind of media the folders contain
	/// </summary>
	public MediaKind MediaKind { get; set; }
}

/// <summary>
///     A single folder to adopt into the library
/// </summary>
public record LibraryImportItem
{
	/// <summary>
	///     Existing folder holding the media files
	/// </summary>
	public string Folder { get; set; } = "";

	/// <summary>
	///     TheTVDB id to add the folder as, when importing Series
	/// </summary>
	public int? TvdbId { get; set; }

	/// <summary>
	///     TheMovieDB id to add the folder as, when importing Movies
	/// </summary>
	public int? TmdbId { get; set; }

	/// <summary>
	///     Quality profile of the created media
	/// </summary>
	public int QualityProfileId { get; set; }

	/// <summary>
	///     Language profile of the created media
	/// </summary>
	public int LanguageProfileId { get; set; }

	/// <summary>
	///     Whether the created media is monitored
	/// </summary>
	public bool MonitorExisting { get; set; } = true;

	/// <summary>
	///     Tags of the created media
	/// </summary>
	public List<string> Tags { get; set; } = new();
}
