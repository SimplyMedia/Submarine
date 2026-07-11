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
	[property: JsonPropertyName("poster_path")] string? PosterPath);

internal record TmdbGenre([property: JsonPropertyName("name")] string Name);

internal record TmdbProductionCompany([property: JsonPropertyName("name")] string Name);

internal record TmdbSearchResponse([property: JsonPropertyName("results")] IReadOnlyList<TmdbMovie> Results);
