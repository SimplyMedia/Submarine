using Submarine.Core.Quality;

namespace Submarine.Core.Parser.Model
{
	public record ParsedRelease
	{
		public string ReleaseTitle { get; set; }
		
		public string SeriesTitle { get; set; }
		
		public ReleaseType Type { get; set; }
		
		public QualityModel Quality { get; set; }
	};
}
