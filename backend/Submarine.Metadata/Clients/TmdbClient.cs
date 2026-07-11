using System.Net.Http.Json;
using System.Text.Json;
using Submarine.Metadata.Contracts;

namespace Submarine.Metadata.Clients;

/// <summary>
///     Typed client for the TMDB API, normalizing responses into <see cref="MovieResource" />
/// </summary>
public class TmdbClient
{
	private const string ImageBaseUrl = "https://image.tmdb.org/t/p/original";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly HttpClient _httpClient;
	private readonly string _apiKey;

	/// <summary>
	///     Creates a new instance of <see cref="TmdbClient" />
	/// </summary>
	/// <param name="httpClient">http client configured with the TMDB base address</param>
	/// <param name="configuration">configuration to read the TMDB api key from</param>
	public TmdbClient(HttpClient httpClient, IConfiguration configuration)
	{
		_httpClient = httpClient;
		_apiKey = configuration["Tmdb:ApiKey"] ?? string.Empty;
	}

	/// <summary>
	///     Searches TMDB for movies matching the given search term
	/// </summary>
	/// <param name="term">search term</param>
	/// <returns>normalized search results</returns>
	public async Task<IReadOnlyList<MovieResource>> SearchMoviesAsync(string term)
	{
		var response = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(
			$"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(term)}", JsonOptions);

		return response?.Results.Select(MapMovie).ToList() ?? [];
	}

	/// <summary>
	///     Gets a single movie from TMDB by its identifier
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the movie</param>
	/// <returns>normalized movie, or null if it could not be found</returns>
	public async Task<MovieResource?> GetMovieAsync(int tmdbId)
	{
		var movie = await _httpClient.GetFromJsonAsync<TmdbMovie>($"movie/{tmdbId}?api_key={_apiKey}", JsonOptions);

		return movie == null ? null : MapMovie(movie);
	}

	private static MovieResource MapMovie(TmdbMovie movie)
	{
		var releaseDate = ParseDate(movie.ReleaseDate);

		return new MovieResource(
			movie.Id,
			movie.ImdbId,
			movie.Title,
			null,
			movie.Overview,
			releaseDate,
			releaseDate?.Year,
			movie.Runtime,
			movie.Genres?.Select(g => g.Name).ToList() ?? [],
			movie.ProductionCompanies?.FirstOrDefault()?.Name,
			movie.PosterPath == null ? null : ImageBaseUrl + movie.PosterPath,
			[]);
	}

	private static DateOnly? ParseDate(string? date)
		=> DateOnly.TryParse(date, out var parsed) ? parsed : null;
}
