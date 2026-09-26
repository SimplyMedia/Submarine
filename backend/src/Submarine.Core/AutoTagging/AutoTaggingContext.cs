using Submarine.Core.Enums;

namespace Submarine.Core.AutoTagging;

/// <summary>
///     The series or movie attributes evaluated against auto tagging rule specifications.
/// </summary>
/// <param name="Genres">Genres.</param>
/// <param name="RootFolderIds">Root folder ids used by any version of the item.</param>
/// <param name="QualityProfileIds">Quality profile ids used by any version of the item.</param>
/// <param name="SeriesType">Series type, null for movies.</param>
/// <param name="Status">Persisted status enum member name.</param>
/// <param name="Year">Release or first air year.</param>
/// <param name="Monitored">Whether the item is monitored.</param>
/// <param name="NetworkOrStudio">Network for series, studio for movies.</param>
/// <param name="OriginalLanguage">Original language code.</param>
/// <param name="Keywords">Metadata keywords, movie only.</param>
public sealed record AutoTaggingContext(
	IReadOnlyList<string> Genres,
	IReadOnlyCollection<int> RootFolderIds,
	IReadOnlyCollection<int> QualityProfileIds,
	SeriesType? SeriesType,
	string Status,
	int? Year,
	bool Monitored,
	string? NetworkOrStudio,
	string? OriginalLanguage = null,
	IReadOnlyList<string>? Keywords = null);
