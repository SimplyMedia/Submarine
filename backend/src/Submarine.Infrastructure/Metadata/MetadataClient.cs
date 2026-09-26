using System.Net.Http.Json;
using System.Net;
using Submarine.Contracts.Metadata;
using Submarine.Core.Common;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     HTTP implementation of <see cref="IMetadataClient" /> against the Metadata service.
/// </summary>
public sealed class MetadataClient(HttpClient http) : IMetadataClient
{
	public async Task<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(string term, MetadataProvider provider, CancellationToken cancellationToken = default)
		=> await GetListAsync<SearchResultResource>(
			$"/api/v1/series/search?term={Uri.EscapeDataString(term)}&provider={provider.ToString().ToLowerInvariant()}",
			cancellationToken);

	public async Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> await GetOrNullAsync<SeriesResource>($"/api/v1/series/tvdb/{tvdbId}", cancellationToken);

	public async Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> await GetOrNullAsync<SeriesResource>($"/api/v1/series/tmdb/{tmdbId}", cancellationToken);

	public async Task<IReadOnlyList<SearchResultResource>> SearchMoviesAsync(string term, int? year = null, CancellationToken cancellationToken = default)
	{
		var url = $"/api/v1/movie/search?term={Uri.EscapeDataString(term)}";
		if (year is not null)
		{
			url += $"&year={year}";
		}

		return await GetListAsync<SearchResultResource>(url, cancellationToken);
	}

	public async Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> await GetOrNullAsync<MovieResource>($"/api/v1/movie/{tmdbId}", cancellationToken);

	public async Task<MovieResource?> GetMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
		=> await GetOrNullAsync<MovieResource>($"/api/v1/movie/imdb/{Uri.EscapeDataString(imdbId)}", cancellationToken);

	public async Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default)
		=> await GetOrNullAsync<CollectionResource>($"/api/v1/collection/{tmdbCollectionId}", cancellationToken);

	public async Task<IReadOnlyList<SearchResultResource>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default)
		=> await GetListAsync<SearchResultResource>($"/api/v1/movie/popular?page={page}", cancellationToken);

	public async Task<IReadOnlyList<SearchResultResource>> GetPopularSeriesAsync(int page = 1, CancellationToken cancellationToken = default)
		=> await GetListAsync<SearchResultResource>($"/api/v1/series/popular?page={page}", cancellationToken);

	public async Task<IReadOnlyList<MovieResource>> GetTmdbListAsync(int listId, CancellationToken cancellationToken = default)
		=> await GetListAsync<MovieResource>($"/api/v1/movie/list/{listId}", cancellationToken);

	public async Task<IReadOnlyList<MovieResource>> GetPersonMoviesAsync(int personId, CancellationToken cancellationToken = default)
		=> await GetListAsync<MovieResource>($"/api/v1/movie/person/{personId}", cancellationToken);

	private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(url, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return default;
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<T>(SubmarineJson.Default, cancellationToken);
	}

	private async Task<IReadOnlyList<T>> GetListAsync<T>(string url, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(url, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return [];
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<List<T>>(SubmarineJson.Default, cancellationToken) ?? [];
	}
}
