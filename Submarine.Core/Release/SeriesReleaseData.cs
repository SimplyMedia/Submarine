using System.Collections.Generic;

namespace Submarine.Core.Release
{
	public record SeriesReleaseData
	{
		public SeriesReleaseType ReleaseType { get; init; }
		
		public IReadOnlyList<int> Seasons { get; init; }
		
		public IReadOnlyList<int> Episodes { get; init; }
	}
}
