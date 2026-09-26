using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Options;
using Submarine.Metadata.Upstream;

namespace Submarine.Metadata;

/// <summary>
///     Endpoint-facing metadata access. Wraps the upstream clients with
///     HybridCache lookups (detail and search TTLs differ) and resolves
///     prefixed search terms like tvdb:123, tmdb:123 or imdb:tt123 into
///     direct detail lookups.
/// </summary>
public sealed class MetadataService(TmdbClient tmdb, TvdbClient tvdb, HybridCache cache, IOptions<CacheOptions> cacheOptions)
{
	public ValueTask<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"series:tvdb:{tvdbId}",
			(tvdb, tvdbId),
			static async (state, token) => await state.tvdb.GetSeriesByTvdbAsync(state.tvdbId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.DetailTtl },
			cancellationToken: cancellationToken);

	public ValueTask<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"series:tmdb:{tmdbId}",
			(tmdb, tmdbId),
			static async (state, token) => await state.tmdb.GetSeriesByTmdbAsync(state.tmdbId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.DetailTtl },
			cancellationToken: cancellationToken);

	public ValueTask<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:{tmdbId}",
			(tmdb, tmdbId),
			static async (state, token) => await state.tmdb.GetMovieAsync(state.tmdbId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.DetailTtl },
			cancellationToken: cancellationToken);

	public ValueTask<MovieResource?> GetMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:imdb:{imdbId}",
			(tmdb, imdbId),
			static async (state, token) => await state.tmdb.GetMovieByImdbAsync(state.imdbId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.DetailTtl },
			cancellationToken: cancellationToken);

	public ValueTask<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"collection:{tmdbCollectionId}",
			(tmdb, tmdbCollectionId),
			static async (state, token) => await state.tmdb.GetCollectionAsync(state.tmdbCollectionId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.DetailTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(
		string term,
		string? provider,
		CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"series:search:{provider ?? "tvdb"}:{term}",
			(self: this, term: term, provider: provider),
			static async (state, token) => await state.self.SearchSeriesCoreAsync(state.term, state.provider, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<SearchResultResource>> SearchMoviesAsync(
		string term,
		int? year,
		CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:search:{term}:{year}",
			(self: this, term: term, year: year),
			static async (state, token) => await state.self.SearchMoviesCoreAsync(state.term, state.year, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<SearchResultResource>> GetPopularMoviesAsync(int page, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:popular:{page}",
			(tmdb, page),
			static async (state, token) => await state.tmdb.GetPopularMoviesAsync(state.page, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<SearchResultResource>> GetPopularSeriesAsync(int page, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"series:popular:{page}",
			(tmdb, page),
			static async (state, token) => await state.tmdb.GetPopularSeriesAsync(state.page, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<MovieResource>> GetMovieListAsync(int listId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:list:{listId}",
			(tmdb, listId),
			static async (state, token) => await state.tmdb.GetMovieListAsync(state.listId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	public ValueTask<IReadOnlyList<MovieResource>> GetMoviesByPersonAsync(int personId, CancellationToken cancellationToken = default)
		=> cache.GetOrCreateAsync(
			$"movie:person:{personId}",
			(tmdb, personId),
			static async (state, token) => await state.tmdb.GetMoviesByPersonAsync(state.personId, token),
			new HybridCacheEntryOptions { Expiration = cacheOptions.Value.SearchTtl },
			cancellationToken: cancellationToken);

	private async Task<IReadOnlyList<SearchResultResource>> SearchSeriesCoreAsync(
		string term,
		string? provider,
		CancellationToken cancellationToken)
	{
		var (prefix, value) = SplitPrefix(term);
		if (provider?.Equals("tmdb", StringComparison.OrdinalIgnoreCase) == true || prefix == "tmdb")
		{
			if (prefix == "tmdb" && int.TryParse(value, out var tmdbId))
				return await FromSeriesAsync(() => tmdb.GetSeriesByTmdbAsync(tmdbId, cancellationToken), "tmdb");
			return await tmdb.SearchSeriesAsync(value, cancellationToken);
		}

		switch (prefix)
		{
			case "tvdb" when int.TryParse(value, out var tvdbId):
				return await FromSeriesAsync(() => tvdb.GetSeriesByTvdbAsync(tvdbId, cancellationToken), "tvdb");
			case "imdb":
				return await tvdb.SearchSeriesAsync(value, cancellationToken);
			default:
				return await tvdb.SearchSeriesAsync(term, cancellationToken);
		}
	}

	private async Task<IReadOnlyList<SearchResultResource>> SearchMoviesCoreAsync(
		string term,
		int? year,
		CancellationToken cancellationToken)
	{
		var (prefix, value) = SplitPrefix(term);
		switch (prefix)
		{
			case "tmdb" when int.TryParse(value, out var tmdbId):
				return await FromMovieAsync(() => tmdb.GetMovieAsync(tmdbId, cancellationToken));
			case "imdb":
				return await FromMovieAsync(() => tmdb.GetMovieByImdbAsync(value, cancellationToken));
			default:
				return await tmdb.SearchMoviesAsync(term, year, cancellationToken);
		}
	}

	private static async Task<IReadOnlyList<SearchResultResource>> FromSeriesAsync(
		Func<Task<SeriesResource?>> lookup,
		string provider)
	{
		var series = await lookup();
		return series is null ? [] : [ToSearchResult(series, provider)];
	}

	private static async Task<IReadOnlyList<SearchResultResource>> FromMovieAsync(
		Func<Task<MovieResource?>> lookup)
	{
		var movie = await lookup();
		return movie is null ? [] : [ToSearchResult(movie)];
	}

	internal static SearchResultResource ToSearchResult(SeriesResource series, string provider) => new(
		series.TvdbId is > 0 ? series.TvdbId : null,
		series.TmdbId,
		series.ImdbId,
		series.Title,
		series.Year,
		series.Overview,
		series.PosterUrl,
		series.Status.ToString(),
		provider);

	internal static SearchResultResource ToSearchResult(MovieResource movie) => new(
		null,
		movie.TmdbId,
		movie.ImdbId,
		movie.Title,
		movie.Year,
		movie.Overview,
		movie.PosterUrl,
		movie.Status.ToString(),
		"tmdb");

	private static (string? Prefix, string Value) SplitPrefix(string term)
	{
		var separator = term.IndexOf(':');
		if (separator <= 0) return (null, term);
		var prefix = term[..separator].ToLowerInvariant();
		return prefix is "tvdb" or "tmdb" or "imdb" ? (prefix, term[(separator + 1)..]) : (null, term);
	}
}
