using Submarine.Core.Config;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.MediaFile;
using Submarine.Core.MediaFile.Naming;
using Submarine.Core.Quality;

namespace Submarine.Api.Services;

/// <summary>
///     Renders library file and folder names from the configured naming templates
/// </summary>
public class MediaNamingService
{
	private readonly NamingTemplateRenderer _renderer;

	public MediaNamingService(NamingTemplateRenderer renderer)
		=> _renderer = renderer;

	public RenderedName RenderEpisodeFile(Series series, IReadOnlyList<Episode> episodes, QualityModel? quality,
		IReadOnlyList<Language> languages, string? releaseGroup, MediaInfo? mediaInfo, NamingConfig config)
	{
		var ordered = episodes.OrderBy(e => e.EpisodeNumber).ToList();
		var isAnime = series.Type == SeriesType.ANIME;

		var context = new SeriesNamingContext
		{
			SeriesTitle = series.Title,
			SeasonNumber = ordered[0].SeasonNumber,
			EpisodeNumbers = ordered.Select(e => e.EpisodeNumber).ToList(),
			AbsoluteEpisodeNumbers = ordered
				.Where(e => e.AbsoluteEpisodeNumber != null)
				.Select(e => e.AbsoluteEpisodeNumber!.Value)
				.ToList(),
			EpisodeTitles = ordered.Select(e => e.Title).ToList(),
			Year = series.Year,
			QualityModel = quality,
			Languages = languages,
			ReleaseGroup = releaseGroup,
			IsAnime = isAnime,
			MediaInfo = mediaInfo
		};

		return _renderer.Render(isAnime ? config.AnimeEpisodeFormat : config.StandardEpisodeFormat, context,
			config.MultiEpisodeStyle);
	}

	public string RenderSeasonFolder(Series series, int seasonNumber, NamingConfig config)
	{
		var context = new SeriesNamingContext
		{
			SeriesTitle = series.Title, SeasonNumber = seasonNumber, Year = series.Year
		};

		return _renderer.Render(config.SeasonFolderFormat, context).Name;
	}

	public RenderedName RenderMovieFile(Movie movie, QualityModel? quality, IReadOnlyList<Language> languages,
		string? releaseGroup, string? edition, MediaInfo? mediaInfo, NamingConfig config)
	{
		var context = new MovieNamingContext
		{
			MovieTitle = movie.Title,
			Year = movie.Year,
			Edition = edition,
			QualityModel = quality,
			Languages = languages,
			ReleaseGroup = releaseGroup,
			MediaInfo = mediaInfo
		};

		return _renderer.Render(config.MovieFormat, context);
	}
}
