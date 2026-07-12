using Submarine.Core.Indexer;
using Submarine.Core.Release;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     A candidate Release considered for download, paired with its Indexer origin
/// </summary>
/// <param name="Release">The parsed Release</param>
/// <param name="Info">The raw Indexer info of the Release, carrying size and seeders</param>
/// <param name="IndexerName">The name of the Indexer the Release originates from, if any</param>
/// <param name="IndexerPriority">The priority of the Indexer, lower is preferred</param>
/// <param name="MinimumSeeders">The minimum amount of seeders the Indexer requires for a torrent Release, if any</param>
public record ReleaseCandidate(
	BaseRelease Release,
	ReleaseInfo Info,
	string? IndexerName,
	int IndexerPriority = 25,
	int? MinimumSeeders = null);
