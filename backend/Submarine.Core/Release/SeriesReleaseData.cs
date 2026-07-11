using System.Collections.Generic;

namespace Submarine.Core.Release;

/// <summary>
///     Series specific Release Data
/// </summary>
public record SeriesReleaseData
{
	/// <summary>
	///     The Type of Series Release
	/// </summary>
	public SeriesReleaseType ReleaseType { get; init; }

	/// <summary>
	///     The Season(s) included in the Release Title, if any
	/// </summary>
	public IReadOnlyList<int> Seasons { get; init; }

	/// <summary>
	///     The Episode(s) included in the Release Title, if any
	/// </summary>
	public IReadOnlyList<int> Episodes { get; init; }

	/// <summary>
	///     The Absolute Episode(s) included in the Release Title, if any
	/// </summary>
	public IReadOnlyList<int> AbsoluteEpisodes { get; init; }
}
