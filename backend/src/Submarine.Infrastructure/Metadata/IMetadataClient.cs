using Submarine.Contracts.Metadata;
using Submarine.Core.Enums;

namespace Submarine.Infrastructure.Metadata;

/// <summary>
///     Typed client for the Submarine.Metadata service.
/// </summary>
public interface IMetadataClient
{
	/// <summary>Search series by title or provider id prefix, for example "tvdb:123".</summary>
	Task<IReadOnlyList<SearchResultResource>> SearchSeriesAsync(string term, MetadataProvider provider, CancellationToken cancellationToken = default);

	/// <summary>Full series detail by TVDB id, null when unknown.</summary>
	Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>Full series detail by TMDB id, null when unknown.</summary>
	Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default);

	/// <summary>Search movies by title and optional year.</summary>
	Task<IReadOnlyList<SearchResultResource>> SearchMoviesAsync(string term, int? year = null, CancellationToken cancellationToken = default);

	/// <summary>Full movie detail by TMDB id, null when unknown.</summary>
	Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default);

	/// <summary>Full movie detail by IMDB id, null when unknown.</summary>
	Task<MovieResource?> GetMovieByImdbAsync(string imdbId, CancellationToken cancellationToken = default);

	/// <summary>A TMDB collection with its movies, null when unknown.</summary>
	Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default);

	/// <summary>Popular movies by page.</summary>
	Task<IReadOnlyList<SearchResultResource>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default);

	/// <summary>Popular series by page.</summary>
	Task<IReadOnlyList<SearchResultResource>> GetPopularSeriesAsync(int page = 1, CancellationToken cancellationToken = default);

	/// <summary>Movies of a TMDB list.</summary>
	Task<IReadOnlyList<MovieResource>> GetTmdbListAsync(int listId, CancellationToken cancellationToken = default);

	/// <summary>Movies of a TMDB person.</summary>
	Task<IReadOnlyList<MovieResource>> GetPersonMoviesAsync(int personId, CancellationToken cancellationToken = default);
}
