using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A Series including a summary of its seasons and total episode count
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
	string Path,
	bool Monitored,
	bool SeasonFolder,
	int QualityProfileId,
	int LanguageProfileId,
	List<string> Tags,
	int EpisodeCount,
	IReadOnlyList<SeriesSeasonResponse> Seasons)
{
	public static SeriesResponse FromSeries(Series series, int episodeCount)
		=> new(series.Id, series.TvdbId, series.TmdbId, series.Title, series.SortTitle, series.Overview,
			series.Network, series.Runtime, series.Year, series.Status, series.Type, series.Path, series.Monitored,
			series.SeasonFolder, series.QualityProfileId, series.LanguageProfileId, series.Tags, episodeCount,
			series.Seasons.Select(s => new SeriesSeasonResponse(s.Id, s.SeasonNumber, s.Monitored)).ToList());
}

/// <summary>
///     Summary of a single season within a <see cref="SeriesResponse" />
/// </summary>
public record SeriesSeasonResponse(int Id, int SeasonNumber, bool Monitored);
