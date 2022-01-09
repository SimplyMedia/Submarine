using System.Collections.Generic;

namespace Submarine.Core.Quality;

public class QualityEdgeCases
{
	/// <summary>
	///     These are default Qualities of groups which do not add there Quality in the Release Group, these will only be used
	///     if no quality is explicitly mentioned in the release
	/// </summary>
	public Dictionary<string, QualitySource> EdgeCaseReleaseGroupQualityMapping { get; } =
		new()
		{
			{ "SubsPlease", QualitySource.WEB_DL },
			{ "Erai-Raws", QualitySource.WEB_DL }
		};
}
