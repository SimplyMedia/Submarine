using System.Text.Json.Serialization;

namespace Submarine.Metadata.Clients;

internal record TvdbResponse<T>([property: JsonPropertyName("data")] T? Data);

internal record TvdbLoginData([property: JsonPropertyName("token")] string Token);

internal record TvdbSearchResult(
	[property: JsonPropertyName("tvdb_id")] string TvdbId,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("year")] string? Year,
	[property: JsonPropertyName("status")] string? Status,
	[property: JsonPropertyName("network")] string? Network,
	[property: JsonPropertyName("image_url")] string? ImageUrl,
	[property: JsonPropertyName("first_air_time")] string? FirstAirTime);

internal record TvdbSeriesExtended(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("status")] TvdbStatus? Status,
	[property: JsonPropertyName("firstAired")] string? FirstAired,
	[property: JsonPropertyName("averageRuntime")] int? AverageRuntime,
	[property: JsonPropertyName("originalNetwork")] TvdbNetwork? OriginalNetwork,
	[property: JsonPropertyName("genres")] IReadOnlyList<TvdbGenre>? Genres,
	[property: JsonPropertyName("image")] string? Image,
	[property: JsonPropertyName("seasons")] IReadOnlyList<TvdbSeason>? Seasons,
	[property: JsonPropertyName("episodes")] IReadOnlyList<TvdbEpisode>? Episodes);

internal record TvdbStatus([property: JsonPropertyName("name")] string? Name);

internal record TvdbNetwork([property: JsonPropertyName("name")] string? Name);

internal record TvdbGenre([property: JsonPropertyName("name")] string Name);

internal record TvdbSeason(
	[property: JsonPropertyName("number")] int Number,
	[property: JsonPropertyName("name")] string? Name,
	[property: JsonPropertyName("type")] TvdbSeasonType? Type);

internal record TvdbSeasonType([property: JsonPropertyName("type")] string? Type);

internal record TvdbEpisode(
	[property: JsonPropertyName("id")] int Id,
	[property: JsonPropertyName("name")] string? Name,
	[property: JsonPropertyName("overview")] string? Overview,
	[property: JsonPropertyName("aired")] string? Aired,
	[property: JsonPropertyName("runtime")] int? Runtime,
	[property: JsonPropertyName("seasonNumber")] int SeasonNumber,
	[property: JsonPropertyName("number")] int Number,
	[property: JsonPropertyName("absoluteNumber")] int? AbsoluteNumber,
	[property: JsonPropertyName("dvdSeason")] int? DvdSeason,
	[property: JsonPropertyName("dvdEpisode")] int? DvdEpisode);
