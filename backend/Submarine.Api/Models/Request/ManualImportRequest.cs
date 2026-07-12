namespace Submarine.Api.Models.Request;

/// <summary>
///     Request to import specific files against user-chosen media and episodes
/// </summary>
public record ManualImportRequest
{
	/// <summary>
	///     Files to import
	/// </summary>
	public List<ManualImportFile> Files { get; set; } = new();

	/// <summary>
	///     Tracked download to mark imported once the files are placed, if any
	/// </summary>
	public int? TrackedDownloadId { get; set; }
}

/// <summary>
///     A single file to import against a chosen media item
/// </summary>
public record ManualImportFile
{
	/// <summary>
	///     Path of the source file
	/// </summary>
	public string Path { get; set; } = "";

	/// <summary>
	///     Id of the Series to import into, mutually exclusive with <see cref="MovieId" />
	/// </summary>
	public int? SeriesId { get; set; }

	/// <summary>
	///     Ids of the Episodes this file satisfies, when importing into a Series
	/// </summary>
	public List<int> EpisodeIds { get; set; } = new();

	/// <summary>
	///     Id of the Movie to import into, mutually exclusive with <see cref="SeriesId" />
	/// </summary>
	public int? MovieId { get; set; }

	/// <summary>
	///     Id of the version to import into
	/// </summary>
	public int MediaVersionId { get; set; }

	/// <summary>
	///     Optional quality override, otherwise the quality is parsed from the file name
	/// </summary>
	public ManualImportQuality? Quality { get; set; }
}

/// <summary>
///     A quality override for a manually imported file
/// </summary>
public record ManualImportQuality
{
	/// <summary>
	///     Quality source name, parsed against the quality source enum
	/// </summary>
	public string? Source { get; set; }

	/// <summary>
	///     Quality resolution name, parsed against the quality resolution enum
	/// </summary>
	public string? Resolution { get; set; }
}
