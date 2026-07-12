using Submarine.Core.Library;
using Submarine.Core.Quality;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A monitored Episode or Movie which is missing a file for at least one monitored version
/// </summary>
/// <param name="Kind">Whether the wanted item is a Series episode or a Movie</param>
/// <param name="MediaId">Id of the Series or Movie</param>
/// <param name="EpisodeId">Id of the Episode, for Series items</param>
/// <param name="Title">Title of the Series or Movie</param>
/// <param name="SeasonNumber">Season number, for Series items</param>
/// <param name="EpisodeNumber">Episode number, for Series items</param>
/// <param name="MissingVersions">Names of the monitored versions still missing a file</param>
public record WantedMissingItem(
	MediaKind Kind,
	int MediaId,
	int? EpisodeId,
	string Title,
	int? SeasonNumber,
	int? EpisodeNumber,
	IReadOnlyList<string> MissingVersions);

/// <summary>
///     A file whose version's Quality Profile cutoff is not yet met by the file's quality
/// </summary>
/// <param name="Kind">Whether the file belongs to a Series episode or a Movie</param>
/// <param name="MediaId">Id of the Series or Movie</param>
/// <param name="EpisodeId">Id of the Episode, for Series items</param>
/// <param name="Title">Title of the Series or Movie</param>
/// <param name="SeasonNumber">Season number, for Series items</param>
/// <param name="EpisodeNumber">Episode number, for Series items</param>
/// <param name="VersionName">Name of the version holding the file</param>
/// <param name="CurrentQuality">Current quality of the file</param>
public record WantedCutoffItem(
	MediaKind Kind,
	int MediaId,
	int? EpisodeId,
	string Title,
	int? SeasonNumber,
	int? EpisodeNumber,
	string VersionName,
	QualityModel CurrentQuality);
