using Submarine.Core.Languages;
using Submarine.Core.Provider;
using Submarine.Core.Quality;
using Submarine.Core.Release;

namespace Submarine.Api.Features.Parse;

/// <summary>A parsed release returned by the parse endpoint.</summary>
/// <param name="FullTitle">The parsed title.</param>
/// <param name="Title">The parsed media title.</param>
/// <param name="Year">The year parsed from the title, if any.</param>
/// <param name="Languages">Languages parsed from the title.</param>
/// <param name="StreamingProvider">Streaming provider parsed from the title, if any.</param>
/// <param name="Quality">The parsed quality.</param>
/// <param name="Protocol">The protocol of the release.</param>
/// <param name="ReleaseGroup">The parsed release group, if any.</param>
/// <param name="HardcodedSubs">Whether the title announces hardcoded subtitles.</param>
/// <param name="Series">Series specific parse results, if any.</param>
/// <param name="Movie">Movie specific parse results, if any.</param>
public sealed record ParsedReleaseResource(
	string FullTitle,
	string Title,
	int? Year,
	IReadOnlyList<Language> Languages,
	StreamingProvider? StreamingProvider,
	QualityResource Quality,
	Protocol Protocol,
	string? ReleaseGroup,
	bool HardcodedSubs,
	ParsedSeriesResource? Series,
	ParsedMovieResource? Movie)
{
	/// <summary>Maps a parsed release to the resource.</summary>
	public static ParsedReleaseResource From(BaseRelease release)
		=> new(
			release.FullTitle,
			release.Title,
			release.Year,
			release.Languages,
			release.StreamingProvider,
			new QualityResource(
				release.Quality.Resolution.Source?.ToString(),
				release.Quality.Resolution.Resolution?.ToString(),
				release.Quality.Resolution.Name,
				release.Quality.Revision.Version,
				release.Quality.Revision.IsRepack,
				release.Quality.Revision.IsProper,
				release.Quality.Revision.IsReal),
			release.Protocol,
			release.ReleaseGroup,
			release.HardcodedSubs,
			release.SeriesReleaseData is null
				? null
				: new ParsedSeriesResource(
					release.SeriesReleaseData.ReleaseType.ToString(),
					release.SeriesReleaseData.Seasons,
					release.SeriesReleaseData.Episodes,
					release.SeriesReleaseData.AbsoluteEpisodes),
			release.MovieReleaseData is null ? null : new ParsedMovieResource(release.MovieReleaseData.Edition));
}

/// <summary>The parsed quality.</summary>
/// <param name="Source">Quality source member name, if any.</param>
/// <param name="Resolution">Resolution member name, if any.</param>
/// <param name="Name">Display name, for example WebDL-1080p.</param>
/// <param name="RevisionVersion">Revision version.</param>
/// <param name="IsRepack">Whether the release is a repack.</param>
/// <param name="IsProper">Whether the release is a proper.</param>
/// <param name="IsReal">Whether the release is a real.</param>
public sealed record QualityResource(
	string? Source,
	string? Resolution,
	string Name,
	int RevisionVersion,
	bool IsRepack,
	bool IsProper,
	bool IsReal);

/// <summary>Series specific parse results.</summary>
/// <param name="ReleaseType">The series release type member name.</param>
/// <param name="Seasons">Seasons parsed from the title.</param>
/// <param name="Episodes">Episodes parsed from the title.</param>
/// <param name="AbsoluteEpisodes">Absolute episodes parsed from the title.</param>
public sealed record ParsedSeriesResource(
	string ReleaseType,
	IReadOnlyList<int> Seasons,
	IReadOnlyList<int> Episodes,
	IReadOnlyList<int> AbsoluteEpisodes);

/// <summary>Movie specific parse results.</summary>
/// <param name="Edition">The edition parsed from the title, if any.</param>
public sealed record ParsedMovieResource(string? Edition);
