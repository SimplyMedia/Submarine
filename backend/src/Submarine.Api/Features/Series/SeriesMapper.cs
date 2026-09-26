using Submarine.Core.Entities;
using Submarine.Core.Library;

namespace Submarine.Api.Features.Series;

/// <summary>
///     Maps series entities to API records.
/// </summary>
public static class SeriesMapper
{
	/// <summary>Map a version entity.</summary>
	public static VersionDto ToDto(MediaVersion version, IReadOnlyDictionary<int, string> rootPaths)
		=> new(
			version.Id,
			version.Name,
			version.SeriesId,
			version.MovieId,
			version.QualityProfileId,
			version.LanguageProfileId,
			version.RootFolderId,
			rootPaths.GetValueOrDefault(version.RootFolderId, string.Empty),
			version.Path,
			version.Monitored);

	/// <summary>Map a series with seasons, episodes, files, versions and tags to the detail record.</summary>
	public static SeriesDetailDto ToDetail(
		Submarine.Core.Entities.Series series,
		IReadOnlyDictionary<int, string> rootPaths,
		IReadOnlyList<string> alternateTitles)
	{
		var now = DateTime.UtcNow;
		var seasons = series.Seasons
			.OrderBy(x => x.SeasonNumber)
			.Select(x =>
			{
				var stats = SeasonStatisticsCalculator.Compute(series.Episodes.Where(e => e.SeasonNumber == x.SeasonNumber));
				return new SeasonDto(
					x.SeasonNumber,
					x.Monitored,
					new SeriesStatisticsDto(stats.EpisodeCount, stats.EpisodeFileCount, stats.TotalEpisodeCount, stats.SizeOnDisk, stats.PercentOfEpisodes));
			})
			.ToList();
		return new SeriesDetailDto(
			ToListItem(series, rootPaths, now),
			seasons,
			[.. alternateTitles]);
	}

	/// <summary>Map a series with versions and tags to the list record.</summary>
	public static SeriesListItemDto ToListItem(Submarine.Core.Entities.Series series, IReadOnlyDictionary<int, string> rootPaths, DateTime now)
	{
		var monitored = series.Episodes.Where(x => x.Monitored).ToList();
		var withFile = monitored.Count(x => x.Files.Count > 0);
		return new SeriesListItemDto(
			series.Id,
			series.TvdbId,
			series.TmdbId,
			series.ImdbId,
			series.Title,
			series.SortTitle,
			series.Overview,
			series.Network,
			series.Runtime,
			series.Year,
			series.PosterUrl,
			series.BackdropUrl,
			series.Status,
			series.Type,
			series.MetadataProvider,
			series.Numbering,
			series.Monitored,
			series.MonitorNewItems,
			series.SeasonFolder,
			[.. series.Genres],
			series.Certification,
			series.FirstAired,
			series.CreatedAt,
			AiringRules.NextAiring(series.Episodes, now)?.AirDateUtc,
			AiringRules.PreviousAiring(series.Episodes, now)?.AirDateUtc,
			[.. series.Tags.Select(x => x.Id)],
			[.. series.Versions.Select(x => ToDto(x, rootPaths))],
			new SeriesStatisticsDto(
				monitored.Count,
				withFile,
				series.Episodes.Count,
				monitored.SelectMany(x => x.Files).Sum(x => x.Size),
				monitored.Count == 0 ? 100.0 : Math.Round(100.0 * withFile / monitored.Count, 1)));
	}
}
