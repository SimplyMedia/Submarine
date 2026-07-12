using System.Text.Json.Serialization;

namespace Submarine.Metadata.Clients;

internal record TmdbMovie(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("imdb_id")] string? ImdbId,
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("release_date")] string? ReleaseDate,
	[property: JsonPropertyName("runtime")] int? Runtime,
	[property: JsonPropertyName("genres")] IReadOnlyList<TmdbGenre>? Genres,
	[property: JsonPropertyName("production_companies")] IReadOnlyList<TmdbProductionCompany>? ProductionCompanies,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath,
	[property: JsonPropertyName("belongs_to_collection")] TmdbBelongsToCollection? BelongsToCollection);

internal record TmdbBelongsToCollection(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string Name);

internal record TmdbCollection(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("parts")] IReadOnlyList<TmdbCollectionPart>? Parts);

internal record TmdbCollectionPart(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("title")] string Title,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("release_date")] string? ReleaseDate,
	[property: JsonPropertyName("poster_path")] string? PosterPath);

internal record TmdbGenre([property: JsonPropertyName("name")] string Name);

internal record TmdbProductionCompany([property: JsonPropertyName("name")] string Name);

internal record TmdbSearchResponse([property: JsonPropertyName("results")] IReadOnlyList<TmdbMovie> Results);

internal record TmdbTvSearchResult(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
	[property: JsonPropertyName("poster_path")] string? PosterPath);

internal record TmdbTvSearchResponse(
	[property: JsonPropertyName("results")] IReadOnlyList<TmdbTvSearchResult> Results);

internal record TmdbSeries(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("status")] string? Status,
	[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
	[property: JsonPropertyName("episode_run_time")] IReadOnlyList<int>? EpisodeRunTime,
	[property: JsonPropertyName("networks")] IReadOnlyList<TmdbNetwork>? Networks,
	[property: JsonPropertyName("genres")] IReadOnlyList<TmdbGenre>? Genres,
	[property: JsonPropertyName("poster_path")] string? PosterPath,
	[property: JsonPropertyName("backdrop_path")] string? BackdropPath,
	[property: JsonPropertyName("seasons")] IReadOnlyList<TmdbSeasonSummary>? Seasons,
	[property: JsonPropertyName("external_ids")] TmdbExternalIds? ExternalIds);

internal record TmdbNetwork([property: JsonPropertyName("name")] string Name);

internal record TmdbExternalIds([property: JsonPropertyName("tvdb_id")] int? TvdbId);

internal record TmdbSeasonSummary(
	[property: JsonPropertyName("season_number")] int SeasonNumber,
	[property: JsonPropertyName("name")] string? Name,
	[property: JsonPropertyName("episode_count")] int EpisodeCount);

internal record TmdbSeasonDetail([property: JsonPropertyName("episodes")] IReadOnlyList<TmdbEpisode>? Episodes);

internal record TmdbEpisode(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string? Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("air_date")] string? AirDate,
	[property: JsonPropertyName("runtime")] int? Runtime,
	[property: JsonPropertyName("season_number")] int SeasonNumber,
	[property: JsonPropertyName("episode_number")] int EpisodeNumber);

internal record TmdbEpisodeGroupsResponse(
	[property: JsonPropertyName("results")] IReadOnlyList<TmdbEpisodeGroupSummary>? Results);

internal record TmdbEpisodeGroupSummary(
	[property: JsonPropertyName("id")] string Id,
	[property: JsonPropertyName("type")] int Type,
	[property: JsonPropertyName("group_count")] int GroupCount);

internal record TmdbEpisodeGroupDetail(
	[property: JsonPropertyName("groups")] IReadOnlyList<TmdbEpisodeGroupSeason>? Groups);

internal record TmdbEpisodeGroupSeason(
	[property: JsonPropertyName("order")] int Order,
	[property: JsonPropertyName("episodes")] IReadOnlyList<TmdbEpisodeGroupEpisode>? Episodes);

internal record TmdbEpisodeGroupEpisode(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("order")] int Order);
