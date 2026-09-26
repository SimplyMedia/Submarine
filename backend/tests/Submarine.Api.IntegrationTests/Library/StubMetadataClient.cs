using Submarine.Contracts.Metadata;
using Submarine.Core.Enums;
using Submarine.Infrastructure.Metadata;

namespace Submarine.Api.IntegrationTests.Library;

/// <summary>
///     In memory IMetadataClient with mutable fixtures for refresh tests.
/// </summary>
public sealed class StubMetadataClient : IMetadataClient
{
	public Dictionary<int, SeriesResource> Series { get; } = new();

	public Dictionary<int, MovieResource> Movies { get; } = new();

	public Dictionary<int, CollectionResource> Collections { get; } = new();

	public Task<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(string term, Core.Enums.MetadataProvider provider, CancellationToken cancellationToken = default)
	{
		if (term.StartsWith("tvdb:", StringComparison.OrdinalIgnoreCase)
			&& int.TryParse(term[5..], out var tvdbId)
			&& Series.TryGetValue(tvdbId, out var byTvdb))
		{
			return Task.FromResult((IReadOnlyList<SearchResultResource>)[Hit(byTvdb)]);
		}

		if (term.StartsWith("tmdb:", StringComparison.OrdinalIgnoreCase)
			&& int.TryParse(term[5..], out var tmdbId))
		{
			var byTmdb = Series.Values.FirstOrDefault(x => x.TmdbId == tmdbId);
			return Task.FromResult((IReadOnlyList<SearchResultResource>)(byTmdb is null ? [] : [Hit(byTmdb)]));
		}

		return Task.FromResult((IReadOnlyList<SearchResultResource>)[.. Series.Values
			.Where(x => x.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
			.Select(Hit)]);
	}

	public Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Series.GetValueOrDefault(tvdbId));

	public Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Series.Values.FirstOrDefault(x => x.TmdbId == tmdbId));

	public Task<IReadOnlyList<SearchResultResource>> SearchMoviesAsync(string term, int? year = null, CancellationToken cancellationToken = default)
		=> Task.FromResult((IReadOnlyList<SearchResultResource>)[.. Movies.Values
			.Where(x => x.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
			.Where(x => !year.HasValue || x.Year == year)
			.Select(Hit)]);

	public Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Movies.GetValueOrDefault(tmdbId));

	public Task<MovieResource?> GetMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Movies.Values.FirstOrDefault(x => x.ImdbId == imdbId));

	public Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default)
		=> Task.FromResult(Collections.GetValueOrDefault(tmdbCollectionId));

	public Task<IReadOnlyList<SearchResultResource>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default)
		=> Task.FromResult((IReadOnlyList<SearchResultResource>)[.. Movies.Values.Select(Hit)]);

	public Task<IReadOnlyList<SearchResultResource>> GetPopularSeriesAsync(int page = 1, CancellationToken cancellationToken = default)
		=> Task.FromResult((IReadOnlyList<SearchResultResource>)[.. Series.Values.Select(Hit)]);

	public Task<IReadOnlyList<MovieResource>> GetTmdbListAsync(int listId, CancellationToken cancellationToken = default)
		=> Task.FromResult((IReadOnlyList<MovieResource>)[.. Movies.Values]);

	public Task<IReadOnlyList<MovieResource>> GetPersonMoviesAsync(int personId, CancellationToken cancellationToken = default)
		=> Task.FromResult((IReadOnlyList<MovieResource>)[.. Movies.Values]);

	private static SearchResultResource Hit(SeriesResource series)
		=> new(series.TvdbId, series.TmdbId, series.ImdbId, series.Title, series.Year, series.Overview, series.PosterUrl, series.Status.ToString(), "tvdb");

	private static SearchResultResource Hit(MovieResource movie)
		=> new(null, movie.TmdbId, movie.ImdbId, movie.Title, movie.Year, movie.Overview, movie.PosterUrl, movie.Status.ToString(), "tmdb");
}
