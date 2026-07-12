using Submarine.Core.Library;
using Submarine.Metadata.Contracts;

namespace Submarine.Api.Clients;

/// <summary>
///     Client for resolving normalized metadata from the Metadata service
/// </summary>
public interface IMetadataClient
{
	/// <summary>
	///     Searches for series matching the given term against the given provider
	/// </summary>
	/// <param name="term">search term</param>
	/// <param name="provider">metadata provider to search</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>matching series</returns>
	Task<IReadOnlyList<SeriesResource>> SearchSeriesAsync(string term,
		MetadataProvider provider = MetadataProvider.TVDB, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets a single series by its TVDB identifier
	/// </summary>
	/// <param name="tvdbId">TheTVDB identifier of the series</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>series if found, otherwise null</returns>
	Task<SeriesResource?> GetSeriesByTvdbAsync(int tvdbId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets a single series by its TMDB identifier
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the series</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>series if found, otherwise null</returns>
	Task<SeriesResource?> GetSeriesByTmdbAsync(int tmdbId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Searches for movies matching the given term
	/// </summary>
	/// <param name="term">search term</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>matching movies</returns>
	Task<IReadOnlyList<MovieResource>> SearchMoviesAsync(string term, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets a single movie by its TMDB identifier
	/// </summary>
	/// <param name="tmdbId">TheMovieDB identifier of the movie</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>movie if found, otherwise null</returns>
	Task<MovieResource?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets a single movie collection by its TMDB identifier
	/// </summary>
	/// <param name="tmdbCollectionId">TheMovieDB identifier of the collection</param>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>collection if found, otherwise null</returns>
	Task<CollectionResource?> GetCollectionAsync(int tmdbCollectionId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Checks whether the Metadata service is reachable
	/// </summary>
	/// <param name="cancellationToken">cancellation token</param>
	/// <returns>true if the service responded successfully</returns>
	Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
