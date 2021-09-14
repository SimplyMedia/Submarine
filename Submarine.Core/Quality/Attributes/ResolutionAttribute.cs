using System;

namespace Submarine.Core.Quality.Attributes
{
	[AttributeUsage(AttributeTargets.Field)]
	public class ResolutionAttribute : Attribute
	{
		public QualityResolution[] Resolutions { get; }
		
		public ResolutionAttribute(params QualityResolution[] resolutions)
		{
			Resolutions = resolutions;
		}
	}
}
