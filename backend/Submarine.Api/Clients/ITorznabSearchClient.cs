using Submarine.Core.Indexer;
using Submarine.Core.Provider;

namespace Submarine.Api.Clients;

/// <summary>
///     Search operations of <see cref="TorznabHttpClient" /> used to query indexers for releases
/// </summary>
public interface ITorznabSearchClient
{
	/// <summary>
	///     Runs a tv search against an indexer
	/// </summary>
	Task<IReadOnlyList<ReleaseInfo>> TvSearchAsync(Provider indexer, int? tvdbId = null, int? season = null,
		int? episode = null, string? query = null, IReadOnlyList<int>? categories = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Runs a movie search against an indexer
	/// </summary>
	Task<IReadOnlyList<ReleaseInfo>> MovieSearchAsync(Provider indexer, int? tmdbId = null, string? imdbId = null,
		string? query = null, IReadOnlyList<int>? categories = null, CancellationToken cancellationToken = default);

	/// <summary>
	///     Runs a free text search against an indexer
	/// </summary>
	Task<IReadOnlyList<ReleaseInfo>> SearchAsync(Provider indexer, string? query = null,
		IReadOnlyList<int>? categories = null, CancellationToken cancellationToken = default);
}
