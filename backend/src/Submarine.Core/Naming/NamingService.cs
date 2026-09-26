using Submarine.Core.Entities;
using Submarine.Core.Enums;

namespace Submarine.Core.Naming;

/// <summary>
///     Renders file and folder names from the naming configuration templates.
///     Episode files pick the standard, daily or anime template from the series type.
/// </summary>
public sealed class NamingService : IFolderNameRenderer
{
	/// <inheritdoc />
	public string RenderSeriesFolder(NamingConfig naming, Series series)
		=> NamingTemplateRenderer.Render(
			naming.SeriesFolderFormat,
			SeriesValues(series),
			naming.ReplaceIllegalCharacters,
			naming.ColonReplacement);

	/// <inheritdoc />
	public string RenderSeasonFolder(NamingConfig naming, Series series, int seasonNumber)
		=> NamingTemplateRenderer.Render(
			seasonNumber == 0 ? naming.SpecialsFolderFormat : naming.SeasonFolderFormat,
			SeriesValues(series) with { SeasonNumber = seasonNumber },
			naming.ReplaceIllegalCharacters,
			naming.ColonReplacement);

	/// <inheritdoc />
	public string RenderMovieFolder(NamingConfig naming, Movie movie)
		=> NamingTemplateRenderer.Render(
			naming.MovieFolderFormat,
			MovieValues(movie),
			naming.ReplaceIllegalCharacters,
			naming.ColonReplacement);

	/// <summary>
	///     Renders the file name for an episode file. The template is chosen by series type; the file name carries no
	///     extension.
	/// </summary>
	/// <param name="series">The owning series.</param>
	/// <param name="episodes">The episodes covered by the file, at least one.</param>
	/// <param name="file">The episode file being named.</param>
	/// <param name="naming">The naming configuration.</param>
	/// <param name="customFormats">Names of the custom formats matching the release, for the custom format tokens.</param>
	public string RenderEpisodeFileName(
		Series series,
		IReadOnlyList<Episode> episodes,
		EpisodeFile file,
		NamingConfig naming,
		IReadOnlyList<string>? customFormats = null)
	{
		return NamingTemplateRenderer.Render(
			EpisodeTemplate(naming, series),
			EpisodeValues(series, episodes, file, customFormats),
			naming.ReplaceIllegalCharacters,
			naming.ColonReplacement,
			naming.MultiEpisodeStyle);
	}

	/// <summary>
	///     Whether the episode file name would carry a placeholder episode title.
	/// </summary>
	public bool UsesPlaceholderTitle(Series series, IReadOnlyList<Episode> episodes, EpisodeFile file, NamingConfig naming)
		=> naming.RenameEpisodes
		   && NamingTemplateRenderer.UsesPlaceholderTitle(EpisodeTemplate(naming, series), EpisodeValues(series, episodes, file, null));

	private static string EpisodeTemplate(NamingConfig naming, Series series)
		=> series.Type switch
		{
			SeriesType.DAILY => naming.DailyEpisodeFormat,
			SeriesType.ANIME => naming.AnimeEpisodeFormat,
			_ => naming.StandardEpisodeFormat
		};

	/// <summary>
	///     Renders the file name for a movie file. The file name carries no extension.
	/// </summary>
	public string RenderMovieFileName(
		Movie movie,
		MovieFile file,
		NamingConfig naming,
		IReadOnlyList<string>? customFormats = null)
		=> NamingTemplateRenderer.Render(
			naming.MovieFormat,
			MovieFileValues(movie, file, customFormats),
			naming.ReplaceIllegalCharacters,
			naming.ColonReplacement);

	private static NamingTokenValues SeriesValues(Series series)
		=> new()
		{
			SeriesTitle = series.Title,
			Year = series.Year,
			TvdbId = series.TvdbId,
			TmdbId = series.TmdbId,
			ImdbId = series.ImdbId
		};

	private static NamingTokenValues MovieValues(Movie movie)
		=> new()
		{
			MovieTitle = movie.Title,
			Year = movie.Year,
			TmdbId = movie.TmdbId,
			ImdbId = movie.ImdbId
		};

	private static NamingTokenValues EpisodeValues(
		Series series,
		IReadOnlyList<Episode> episodes,
		EpisodeFile file,
		IReadOnlyList<string>? customFormats)
	{
		var first = episodes.Count > 0 ? episodes[0] : null;

		return new NamingTokenValues
		{
			SeriesTitle = series.Title,
			Year = series.Year,
			TvdbId = series.TvdbId,
			TmdbId = series.TmdbId,
			ImdbId = series.ImdbId,
			SeasonNumber = first?.SeasonNumber ?? 0,
			EpisodeNumbers = [.. episodes.Select(episode => episode.EpisodeNumber)],
			AbsoluteEpisodeNumbers = [.. episodes
				.Where(episode => episode.AbsoluteEpisodeNumber is not null)
				.Select(episode => episode.AbsoluteEpisodeNumber!.Value)],
			EpisodeTitles = [.. episodes.Select(episode => episode.Title)],
			AirDate = first?.AirDate,
			Quality = file.Quality,
			Languages = file.Languages,
			ReleaseGroup = file.ReleaseGroup,
			Edition = file.Edition,
			MediaInfo = TokenMediaInfo(file.MediaInfo),
			OriginalTitle = file.SceneName,
			OriginalFilename = FileNameOf(file.RelativePath),
			CustomFormats = customFormats ?? []
		};
	}

	private static NamingTokenValues MovieFileValues(Movie movie, MovieFile file, IReadOnlyList<string>? customFormats)
		=> MovieValues(movie) with
		{
			Quality = file.Quality,
			Languages = file.Languages,
			ReleaseGroup = file.ReleaseGroup,
			Edition = file.Edition,
			MediaInfo = TokenMediaInfo(file.MediaInfo),
			OriginalTitle = file.SceneName,
			OriginalFilename = FileNameOf(file.RelativePath),
			CustomFormats = customFormats ?? []
		};

	private static MediaInfoTokenValues? TokenMediaInfo(Entities.MediaInfoModel? mediaInfo)
		=> mediaInfo is null
			? null
			: new MediaInfoTokenValues
			{
				VideoCodec = mediaInfo.VideoCodec,
				AudioCodec = mediaInfo.AudioCodec,
				AudioChannels = mediaInfo.AudioChannels,
				VideoDynamicRange = mediaInfo.VideoDynamicRange,
				VideoBitDepth = mediaInfo.VideoBitDepth,
				AudioLanguages = mediaInfo.AudioLanguages ?? [],
				SubtitleLanguages = mediaInfo.SubtitleLanguages ?? []
			};

	private static string FileNameOf(string relativePath)
	{
		var separator = relativePath.LastIndexOfAny(['/', '\\']);

		return separator < 0 ? relativePath : relativePath[(separator + 1)..];
	}
}
