using Submarine.Core.DecisionEngine;

namespace Submarine.Core.MediaFiles;

/// <summary>
///     Checks releases against the blocklist. A release is blocklisted when its guid matches, when its
///     torrent info hash matches, or when its title matches for the same indexer.
/// </summary>
public interface IBlocklistService
{
	/// <summary>
	///     Whether a specific release is blocklisted.
	/// </summary>
	/// <param name="releaseTitle">Release title.</param>
	/// <param name="guid">Indexer guid of the release, if known.</param>
	/// <param name="indexerId">Id of the indexer the release came from, if known.</param>
	/// <param name="infoHash">Torrent info hash of the release, if known.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task<bool> IsBlocklistedAsync(
		string releaseTitle,
		string? guid,
		int? indexerId,
		string? infoHash,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Loads the current blocklist once and returns a synchronous predicate suitable for
	///     <see cref="DecisionContext.IsBlocklisted" />.
	/// </summary>
	Task<Func<ReleaseCandidate, bool>> BuildPredicateAsync(CancellationToken cancellationToken = default);
}
