using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A Series including a summary of its seasons, versions and total episode count
/// </summary>
public record SeriesResponse(
	int Id,
	int TvdbId,
	int? TmdbId,
	string Title,
	string? SortTitle,
	string? Overview,
	string? Network,
	int? Runtime,
	int? Year,
	SeriesStatus Status,
	SeriesType Type,
	MetadataProvider MetadataProvider,
	EpisodeNumbering Numbering,
	bool Monitored,
	bool SeasonFolder,
	List<string> Tags,
	int EpisodeCount,
	IReadOnlyList<SeriesSeasonResponse> Seasons,
	IReadOnlyList<MediaVersionResponse> Versions)
{
	public static SeriesResponse FromSeries(Series series, int episodeCount)
		=> new(series.Id, series.TvdbId, series.TmdbId, series.Title, series.SortTitle, series.Overview,
			series.Network, series.Runtime, series.Year, series.Status, series.Type, series.MetadataProvider,
			series.Numbering, series.Monitored,
			series.SeasonFolder, series.Tags, episodeCount,
			series.Seasons.Select(s => new SeriesSeasonResponse(s.Id, s.SeasonNumber, s.Monitored)).ToList(),
			series.Versions.Select(MediaVersionResponse.FromVersion).ToList());
}

/// <summary>
///     Summary of a single season within a <see cref="SeriesResponse" />
/// </summary>
public record SeriesSeasonResponse(int Id, int SeasonNumber, bool Monitored);
