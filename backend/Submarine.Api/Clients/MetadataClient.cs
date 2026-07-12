using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Clients;

/// <summary>
///     Http implementation of <see cref="IMetadataClient" />
/// </summary>
public class MetadataClient : IMetadataClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly HttpClient _httpClient;

	/// <summary>
	///     Creates a new instance of <see cref="MetadataClient" />
	/// </summary>
	/// <param name="httpClient">Http client to resolve metadata with</param>
	public MetadataClient(HttpClient httpClient)
		=> _httpClient = httpClient;

	/// <inheritdoc />
	public async Task<IReadOnlyList<SeriesResource>> SearchSeriesAsync(string term,
		MetadataProvider provider = MetadataProvider.TVDB, CancellationToken cancellationToken = default)
	{
		var query = $"api/v1/series/search?term={Uri.EscapeDataString(term)}";

		if (provider == MetadataProvider.TMDB)
			query += "&provider=tmdb";

		var results = await _httpClient.GetFromJsonAsync<IReadOnlyList<SeriesResource>>(
			query, JsonOptions, cancellationToken);

		return results ?? Array.Empty<SeriesResource>();
	}

	/// <inheritdoc />
	public Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> GetSeriesAsync($"api/v1/series/tvdb/{tvdbId}", cancellationToken);

	/// <inheritdoc />
	public Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> GetSeriesAsync($"api/v1/series/tmdb/{tmdbId}", cancellationToken);

	private async Task<SeriesResource?> GetSeriesAsync(string path, CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync(path, cancellationToken);

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<SeriesResource>(JsonOptions, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<MovieResource>> SearchMoviesAsync(string term,
		CancellationToken cancellationToken = default)
	{
		var results = await _httpClient.GetFromJsonAsync<IReadOnlyList<MovieResource>>(
			$"api/v1/movie/search?term={Uri.EscapeDataString(term)}", JsonOptions, cancellationToken);

		return results ?? Array.Empty<MovieResource>();
	}

	/// <inheritdoc />
	public async Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.GetAsync($"api/v1/movie/{tmdbId}", cancellationToken);

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<MovieResource>(JsonOptions, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId,
		CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.GetAsync($"api/v1/collection/{tmdbCollectionId}", cancellationToken);

		if (response.StatusCode == HttpStatusCode.NotFound)
			return null;

		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<CollectionResource>(JsonOptions, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

			using var response = await _httpClient.GetAsync("_status/healthz", linked.Token);

			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
