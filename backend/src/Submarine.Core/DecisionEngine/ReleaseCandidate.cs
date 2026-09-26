using Submarine.Core.Indexers;
using Submarine.Core.Release;

namespace Submarine.Core.DecisionEngine;

/// <summary>
///     A candidate release considered for download: the raw indexer release, its parsed form and the library items it
///     matched, if any.
/// </summary>
/// <param name="Parsed">The parsed release.</param>
/// <param name="Info">The raw release as reported by the indexer, carrying size, seeders, publish date and indexer origin.</param>
/// <param name="MinimumSeeders">The minimum amount of seeders the indexer requires for a torrent release, if any.</param>
/// <param name="MatchedSeriesId">Id of the matched series, if any.</param>
/// <param name="MatchedMovieId">Id of the matched movie, if any.</param>
/// <param name="EpisodeIds">Ids of the matched episodes, if any.</param>
public sealed record ReleaseCandidate(
	BaseRelease Parsed,
	ReleaseInfo Info,
	int? MinimumSeeders = null,
	int? MatchedSeriesId = null,
	int? MatchedMovieId = null,
	IReadOnlyList<int>? EpisodeIds = null);
