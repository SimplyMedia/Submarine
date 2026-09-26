using System;
using System.Collections.Generic;

namespace Submarine.Core.Quality;

/// <summary>
///     Constants related to QualityEdgeCases
/// </summary>
public static class QualityEdgeCasesConstants
{
	/// <summary>
	///     These are default Qualities of groups which do not add there Quality in the Release Group, these will only be used
	///     if no quality is explicitly mentioned in the release
	/// </summary>
	public static Dictionary<string, QualitySource> EdgeCaseReleaseGroupQualitySourceMapping { get; } =
		new(StringComparer.OrdinalIgnoreCase)
		{
			{ "SubsPlease", QualitySource.WEB_DL },
			{ "HorribleSubs", QualitySource.WEB_DL },
			{ "Erai-Raws", QualitySource.WEB_DL }
		};
}
