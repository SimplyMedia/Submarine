using System.Collections.Generic;
using System.Linq;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Quality
{
	public class QualityResolutionModel
	{
		public static QualityResolutionModel[] All { get; } = EnumUtil.GetEnumValues<QualitySource>()
			.Select(source =>
			{
				var attribute = source.GetAttribute<ResolutionAttribute>();

				return attribute != null
					? attribute.Resolutions.Select(resolution => new QualityResolutionModel(source, resolution))
					: new List<QualityResolutionModel>
					{
						new(source)
					};
			})
			.ToList()
			.SelectMany(i => i)
			.ToArray();
		
		public QualitySource QualitySource { get; }
		
		public QualityResolution? Resolution { get; }

		public string Name
			=> $"{QualitySourceName ?? QualitySource.ToString()}{(Resolution != null ? $"-{Resolution}p" : "")}";
		
		private string? QualitySourceName { get; }

		public QualityResolutionModel(QualitySource qualitySource, QualityResolution? resolution = null)
		{
			var displayNameAttribute = qualitySource.GetAttribute<NameAttribute>();

			if (displayNameAttribute != null)
				QualitySourceName = displayNameAttribute.Name;
			
			QualitySource = qualitySource;
			Resolution = resolution;
		}
	}
}
