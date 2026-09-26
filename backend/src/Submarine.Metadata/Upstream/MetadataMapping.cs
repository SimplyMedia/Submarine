using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Contracts.Metadata;

namespace Submarine.Metadata.Upstream;

/// <summary>
///     Upstream payload shapes for TMDB v3 and TVDB v4 plus the mapping into
///     <see cref="Contracts.Metadata" /> resources.
/// </summary>
internal static class MetadataMapping
{
	public const string TmdbImageBase = "https://image.tmdb.org/t/p/original";

	public static DateOnly? ParseDate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		return DateTime.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var parsed)
			? DateOnly.FromDateTime(parsed)
			: null;
	}

	public static string? TmdbImage(string? path)
		=> string.IsNullOrEmpty(path) ? null : $"{TmdbImageBase}{path}";

	public static string? ToSortTitle(string? title)
	{
		if (string.IsNullOrWhiteSpace(title)) return null;
		var trimmed = title.Trim();
		foreach (var article in (string[])["the ", "a ", "an "])
		{
			if (trimmed.Length <= article.Length
				|| !trimmed.StartsWith(article, StringComparison.OrdinalIgnoreCase)) continue;
			return $"{trimmed[article.Length..].Trim()}, {article.Trim()}".ToLowerInvariant();
		}

		return trimmed.ToLowerInvariant();
	}

	public static SeriesStatus MapTvdbStatus(string? status) => status?.ToLowerInvariant() switch
	{
		"continuing" => SeriesStatus.CONTINUING,
		"ended" => SeriesStatus.ENDED,
		"upcoming" => SeriesStatus.UPCOMING,
		_ => SeriesStatus.UNKNOWN
	};

	public static SeriesStatus MapTmdbStatus(string? status) => status?.ToLowerInvariant() switch
	{
		"returning series" => SeriesStatus.CONTINUING,
		"ended" or "canceled" or "cancelled" => SeriesStatus.ENDED,
		"planned" or "in production" or "pilot" => SeriesStatus.UPCOMING,
		_ => SeriesStatus.UNKNOWN
	};

	/// <summary>
	///     Movies are released once digital or physical is out, in cinemas once
	///     the theatrical date passed, announced before that.
	/// </summary>
	public static MovieStatus DeriveMovieStatus(DateOnly? inCinemas, DateOnly? digital, DateOnly? physical, DateOnly today)
	{
		var homeRelease = MinDate(digital, physical);
		if (homeRelease is { } released && released <= today) return MovieStatus.RELEASED;
		if (inCinemas is { } cinematic && cinematic <= today) return MovieStatus.IN_CINEMAS;
		return MovieStatus.ANNOUNCED;
	}

	public static DateOnly? MinDate(params DateOnly?[] dates) =>
		dates.Where(d => d is not null).Cast<DateOnly?>().MinOrNull();

	internal static DateOnly? MinOrNull(this IEnumerable<DateOnly?> source)
	{
		DateOnly? result = null;
		foreach (var value in source)
			if (result is null || value < result) result = value;
		return result;
	}
}

// TMDB v3 shapes. Field names use JsonPropertyName because upstream is snake_case.

internal sealed record TmdbGenre(string? Name);

internal sealed record TmdbNetwork(string? Name);

internal sealed record TmdbProductionCompany(string? Name);

internal sealed record TmdbSeasonSummary(
	[property: JsonPropertyName("season_number")] int SeasonNumber,
	string? Name,
	[property: JsonPropertyName("episode_count")] int? EpisodeCount);

internal sealed record TmdbExternalIds(
	[property: JsonPropertyName("tvdb_id")] int? TvdbId,
	[property: JsonPropertyName("imdb_id")] string? ImdbId);

internal sealed record TmdbAlternativeTitle(string? Title, [property: JsonPropertyName("iso_3166_1")] string? Iso31661);

internal sealed record TmdbAlternativeTitles(IReadOnlyList<TmdbAlternativeTitle>? Results);

internal sealed record TmdbContentRating(string Rating, [property: JsonPropertyName("iso_3166_1")] string Iso31661);

internal sealed record TmdbContentRatings(IReadOnlyList<TmdbContentRating>? Results);

internal sealed record TmdbEpisodeGroupSummary(string Id, string? Name, int Type);

internal sealed record TmdbEpisodeGroups(IReadOnlyList<TmdbEpisodeGroupSummary>? Results);

internal sealed record TmdbEpisodeGroupEpisode(
	string? Id,
	int? Order,
	[property: JsonPropertyName("season_number")] int? SeasonNumber,
	[property: JsonPropertyName("episode_number")] int? EpisodeNumber,
	string? Name);

internal sealed record TmdbEpisodeGroupGroup(int Order, IReadOnlyList<TmdbEpisodeGroupEpisode> Episodes);

internal sealed record TmdbEpisodeGroupDetail(IReadOnlyList<TmdbEpisodeGroupGroup> Groups);

internal sealed record TmdbEpisode(
	int Id,
	string? Name,
	string? Overview,
	[property: JsonPropertyName("air_date")] string? AirDate,
	int? Runtime,
	[property: JsonPropertyName("season_number")] int? SeasonNumber,
	[property: JsonPropertyName("episode_number")] int? EpisodeNumber,
	[property: JsonPropertyName("still_path")] string? StillPath);

internal sealed record TmdbSeason(
	int Id,
	string? Name,
	string? Overview,
	[property: JsonPropertyName("air_date")] string? AirDate,
	[property: JsonPropertyName("season_number")] int? SeasonNumber,
	IReadOnlyList<TmdbEpisode>? Episodes);

internal sealed record TmdbTvSummary(
	int Id,
	string? Name,
	string? Overview,
	[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath);

internal sealed record TmdbTvDetail(
	int Id,
	string? Name,
	[property: JsonPropertyName("original_name")] string? OriginalName,
	string? Overview,
	[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
	string? Status,
	[property: JsonPropertyName("episode_run_time")] IReadOnlyList<int>? EpisodeRunTime,
	IReadOnlyList<TmdbGenre>? Genres,
	IReadOnlyList<TmdbNetwork>? Networks,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath,
	IReadOnlyList<TmdbSeasonSummary>? Seasons,
	[property: JsonPropertyName("external_ids")] TmdbExternalIds? ExternalIds,
	[property: JsonPropertyName("alternative_titles")] TmdbAlternativeTitles? AlternativeTitles,
	[property: JsonPropertyName("content_ratings")] TmdbContentRatings? ContentRatings,
	[property: JsonPropertyName("episode_groups")] TmdbEpisodeGroups? EpisodeGroups);

internal sealed record TmdbReleaseDate(
	string? Certification,
	string? Note,
	[property: JsonPropertyName("release_date")] string? ReleaseDate,
	int? Type);

internal sealed record TmdbReleaseDateResult(
	[property: JsonPropertyName("iso_3166_1")] string Iso31661,
	[property: JsonPropertyName("release_dates")] IReadOnlyList<TmdbReleaseDate> ReleaseDates);

internal sealed record TmdbReleaseDates(IReadOnlyList<TmdbReleaseDateResult> Results);

internal sealed record TmdbVideo(string? Key, string? Site, string? Type, bool? Official);

internal sealed record TmdbVideos(IReadOnlyList<TmdbVideo> Results);

internal sealed record TmdbBelongsToCollection(int Id, string? Name);

internal sealed record TmdbMovieSummary(
	int Id,
	string? Title,
	string? Overview,
	[property: JsonPropertyName("release_date")] string? ReleaseDate,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath,
	[property: JsonPropertyName("original_title")] string? OriginalTitle);

internal sealed record TmdbMovieDetail(
	int Id,
	string? Title,
	[property: JsonPropertyName("original_title")] string? OriginalTitle,
	string? Overview,
	[property: JsonPropertyName("release_date")] string? ReleaseDate,
	int? Runtime,
	IReadOnlyList<TmdbGenre>? Genres,
	[property: JsonPropertyName("production_companies")] IReadOnlyList<TmdbProductionCompany>? ProductionCompanies,
	string? Status,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath,
	[property: JsonPropertyName("belongs_to_collection")] TmdbBelongsToCollection? BelongsToCollection,
	[property: JsonPropertyName("release_dates")] TmdbReleaseDates? ReleaseDates,
	[property: JsonPropertyName("alternative_titles")] TmdbAlternativeTitles? AlternativeTitles,
	TmdbVideos? Videos,
	[property: JsonPropertyName("external_ids")] TmdbExternalIds? ExternalIds);

internal sealed record TmdbCollectionDetail(
	int Id,
	string? Name,
	string? Overview,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	IReadOnlyList<TmdbMovieSummary>? Parts);

internal sealed record TmdbFindResults(
	[property: JsonPropertyName("movie_results")] IReadOnlyList<TmdbMovieSummary>? MovieResults,
	[property: JsonPropertyName("tv_results")] IReadOnlyList<TmdbTvSummary>? TvResults);

internal sealed record TmdbPage<T>(int? Page, IReadOnlyList<T>? Results);

internal sealed record TmdbList(int Id, string? Name, string? Overview, IReadOnlyList<TmdbMovieSummary> Items);

// TVDB v4 shapes. Upstream is camelCase which matches the web serializer defaults.

internal sealed record TvdbLoginRequest(string ApiKey, string? Pin = null);

internal sealed record TvdbEnvelope<T>(T? Data);

internal sealed record TvdbLoginData(string? Token);

internal sealed record TvdbStatusInfo(string? Name);

internal sealed record TvdbGenreEntry(string? Name);

internal sealed record TvdbAlias(string? Language, string? Alias);

internal sealed record TvdbSeasonType(string? Name);

internal sealed record TvdbSeasonEntry(
	int Id,
	int SeasonNumber,
	TvdbSeasonType? Type,
	int? EpisodeCount);

internal sealed record TvdbCompany(string? Name, TvdbSeasonType? Type);

internal sealed record TvdbRemoteId(JsonElement Id, TvdbSeasonType? Type);

internal sealed record TvdbEpisodeEntry(
	int Id,
	int? SeasonNumber,
	int? Number,
	[property: JsonPropertyName("absoluteNumber")] int? AbsoluteNumber,
	string? Name,
	string? Overview,
	string? Aired,
	int? Runtime,
	string? Image);

internal sealed record TvdbLinks(string? Next);

internal sealed record TvdbEpisodesPage(IReadOnlyList<TvdbEpisodeEntry>? Episodes, TvdbLinks? Links);

internal sealed record TvdbTranslation(string? Language, string? Name);

internal sealed record TvdbTranslations(
	[property: JsonPropertyName("nameTranslations")] IReadOnlyList<TvdbTranslation>? NameTranslations);

internal sealed record TvdbSeriesDetail(
	int Id,
	string? Name,
	string? Overview,
	[property: JsonPropertyName("firstAired")] string? FirstAired,
	TvdbStatusInfo? Status,
	[property: JsonPropertyName("averageRuntime")] int? AverageRuntime,
	string? Poster,
	IReadOnlyList<string>? Backdrops,
	IReadOnlyList<TvdbGenreEntry>? Genres,
	IReadOnlyList<TvdbAlias>? Aliases,
	IReadOnlyList<TvdbCompany>? Companies,
	IReadOnlyList<TvdbRemoteId>? RemoteIds,
	IReadOnlyList<TvdbSeasonEntry>? Seasons);

internal sealed record TvdbSearchItem(
	[property: JsonPropertyName("tvdbId")] int? TvdbId,
	string? Type,
	string? Name,
	string? Year,
	string? Overview,
	[property: JsonPropertyName("image_url")] string? ImageUrl,
	TvdbStatusInfo? Status);
