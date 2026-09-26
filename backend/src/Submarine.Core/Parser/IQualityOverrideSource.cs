using System.Collections.Generic;
using Submarine.Core.Quality;

namespace Submarine.Core.Parser;

/// <summary>
///     Provides the merged view of release group quality source overrides (built-in defaults merged with user-defined
///     overrides), keys are case-insensitive
/// </summary>
public interface IQualityOverrideSource
{
	/// <summary>
	///     Release group to Quality Source mapping applied when a release does not state a quality
	/// </summary>
	IReadOnlyDictionary<string, QualitySource> Overrides { get; }
}
