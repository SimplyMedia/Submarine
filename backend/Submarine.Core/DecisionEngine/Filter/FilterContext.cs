using Submarine.Core.Languages;
using Submarine.Core.Quality;
using Submarine.Core.Release;

namespace Submarine.Core.DecisionEngine.Filter;

/// <summary>
///     The Field values of a candidate Release, evaluated by the <see cref="FilterEvaluator" />
/// </summary>
/// <param name="ReleaseGroup">The Release Group of the Release, if any</param>
/// <param name="IndexerName">The name of the Indexer the Release originates from, if any</param>
/// <param name="Quality">The name of the Quality of the Release, if any</param>
/// <param name="Languages">The Languages of the Release</param>
/// <param name="Source">The Streaming Provider of the Release, if any</param>
public record FilterContext(
	string? ReleaseGroup,
	string? IndexerName,
	string? Quality,
	IReadOnlyList<Language> Languages,
	StreamingProvider? Source)
{
	/// <summary>
	///     Builds a <see cref="FilterContext" /> from a Release and its Indexer name
	/// </summary>
	/// <param name="release">The Release to build the context from</param>
	/// <param name="indexerName">The name of the Indexer the Release originates from, if any</param>
	/// <returns>The <see cref="FilterContext" /> for the Release</returns>
	public static FilterContext From(BaseRelease release, string? indexerName)
		=> new(
			release.ReleaseGroup,
			indexerName,
			release.Quality.Resolution.Name,
			release.Languages,
			release.StreamingProvider);
}
