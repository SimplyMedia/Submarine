using System.Collections.Generic;
using Submarine.Core.Parser;
using Submarine.Core.Quality;

namespace Submarine.Core.Tests.Parser;

/// <summary>
///     Fixed in-memory override source, defaults to the built-in edge case mapping
/// </summary>
public class StaticQualityOverrideSource : IQualityOverrideSource
{
	public StaticQualityOverrideSource(IReadOnlyDictionary<string, QualitySource>? overrides = null)
		=> Overrides = overrides ?? QualityEdgeCasesConstants.EdgeCaseReleaseGroupQualitySourceMapping;

	public IReadOnlyDictionary<string, QualitySource> Overrides { get; }
}
