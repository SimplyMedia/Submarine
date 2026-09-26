using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Languages;
using Submarine.Core.Quality;

namespace Submarine.Core.Naming;

/// <summary>
///     One rendered naming example.
/// </summary>
/// <param name="Name">Name of the template the example renders, for example StandardEpisodeFormat.</param>
/// <param name="Preview">The rendered sample.</param>
public sealed record NamingSample(string Name, string Preview);

/// <summary>
///     Builds sample renderings of every naming template.
/// </summary>
public static class NamingPreview
{
	/// <summary>
	///     Renders one sample per naming template using sample media.
	/// </summary>
	/// <param name="naming">The naming configuration to render with.</param>
	/// <param name="namingService">The naming service.</param>
	/// <param name="customFormats">Names of custom formats shown in the custom format tokens.</param>
	public static IReadOnlyList<NamingSample> Samples(
		NamingConfig naming,
		NamingService namingService,
		IReadOnlyList<string>? customFormats = null)
	{
		var standardSeries = new Series
		{
			Title = "The Series",
			Year = 2010,
			TvdbId = 123456,
			Type = SeriesType.STANDARD
		};
		var standardEpisodes = new[]
		{
			new Episode { SeasonNumber = 1, EpisodeNumber = 5, Title = "The Title" }
		};

		var dailySeries = new Series
		{
			Title = "The Series",
			Year = 2010,
			TvdbId = 123456,
			Type = SeriesType.DAILY
		};
		var dailyEpisodes = new[]
		{
			new Episode { SeasonNumber = 1, EpisodeNumber = 5, Title = "The Title", AirDate = "2021-05-12" }
		};

		var animeSeries = new Series
		{
			Title = "Some Anime",
			TvdbId = 123456,
			Type = SeriesType.ANIME
		};
		var animeEpisodes = new[]
		{
			new Episode { SeasonNumber = 1, EpisodeNumber = 15, AbsoluteEpisodeNumber = 15, Title = "The Battle" }
		};

		var movie = new Movie { Title = "Some Movie", Year = 2020, TmdbId = 654321 };

		return
		[
			new NamingSample("StandardEpisodeFormat", namingService.RenderEpisodeFileName(
				standardSeries, standardEpisodes, SampleEpisodeFile(), naming, customFormats)),
			new NamingSample("DailyEpisodeFormat", namingService.RenderEpisodeFileName(
				dailySeries, dailyEpisodes, SampleEpisodeFile(), naming, customFormats)),
			new NamingSample("AnimeEpisodeFormat", namingService.RenderEpisodeFileName(
				animeSeries, animeEpisodes, SampleEpisodeFile(), naming, customFormats)),
			new NamingSample("MultiEpisodeFormat", namingService.RenderEpisodeFileName(
				standardSeries,
				[standardEpisodes[0], new() { SeasonNumber = 1, EpisodeNumber = 6, Title = "The Sequel" }],
				SampleEpisodeFile(),
				naming,
				customFormats)),
			new NamingSample("SeriesFolderFormat", namingService.RenderSeriesFolder(naming, standardSeries)),
			new NamingSample("SeasonFolderFormat", namingService.RenderSeasonFolder(naming, standardSeries, 1)),
			new NamingSample("MovieFormat", namingService.RenderMovieFileName(
				movie, SampleMovieFile(), naming, customFormats)),
			new NamingSample("MovieFolderFormat", namingService.RenderMovieFolder(naming, movie))
		];
	}

	private static EpisodeFile SampleEpisodeFile()
		=> new()
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R1080_P),
				new Revision()),
			Languages = [Language.ENGLISH],
			ReleaseGroup = "GROUP",
			RelativePath = "Series.Title.S01E05.1080p.WEB-DL-GROUP.mkv",
			SceneName = "Series.Title.S01E05.1080p.WEB-DL-GROUP"
		};

	private static MovieFile SampleMovieFile()
		=> new()
		{
			Quality = new QualityModel(
				new QualityResolutionModel(QualitySource.WEB_DL, QualityResolution.R2160_P),
				new Revision()),
			Languages = [Language.ENGLISH],
			ReleaseGroup = "GROUP",
			RelativePath = "Movie.Title.2020.2160p.WEB-DL-GROUP.mkv",
			SceneName = "Movie.Title.2020.2160p.WEB-DL-GROUP"
		};
}
