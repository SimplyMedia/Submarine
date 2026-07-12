namespace Submarine.Api.Models.Request;

/// <summary>
///     Request to add all missing movies of a collection to the library
/// </summary>
public record AddCollectionMoviesRequest
{
	public int QualityProfileId { get; set; }

	public int LanguageProfileId { get; set; }

	public int? RootFolderId { get; set; }

	public bool Monitored { get; set; } = true;

	/// <summary>
	///     Whether to run an automatic search for each added movie right after it is added
	/// </summary>
	public bool SearchOnAdd { get; set; }
}
