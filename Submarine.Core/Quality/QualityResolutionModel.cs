using System;
using System.Linq;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Quality
{
	public class QualityResolutionModel
	{
		public static QualityResolutionModel[] All { get; } = Enum.GetValues<QualitySource>()
			.Select(s =>
				s.GetAttribute<ResolutionAttribute>()?.Resolutions
					.Select(r => new QualityResolutionModel(s, r))
				?? new[] {new QualityResolutionModel(s)})
			.SelectMany(i => i)
			.ToArray();

		public QualitySource? QualitySource { get; }

		public QualityResolution? Resolution { get; }

		public string Name
			=> $"{QualitySourceName ?? QualitySource.ToString()}{(Resolution != null ? $"-{Resolution}p" : "")}";

		private string? QualitySourceName { get; }

		public QualityResolutionModel(QualitySource? qualitySource = null, QualityResolution? resolution = null)
		{
			QualitySourceName = qualitySource?.GetAttribute<NameAttribute>()?.Name;

			QualitySource = qualitySource;
			Resolution = resolution;
		}
	}
}
