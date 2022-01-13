using System;
using System.Linq;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Quality;

public class QualityResolutionModel
{
	public static QualityResolutionModel[] All { get; } = Enum.GetValues<QualitySource>()
		.Select(s =>
			s.GetAttribute<ResolutionAttribute>()?.Resolutions
				.Select(r => new QualityResolutionModel(s, r))
			?? new[] { new QualityResolutionModel(s) })
		.SelectMany(i => i)
		.ToArray();

	public QualitySource? Source { get; }

	public QualityResolution? Resolution { get; }

	public string Name
		=> $"{QualitySourceName ?? Source.ToString()}{(Resolution != null ? $"-{ResolutionHumanReadable}" : "")}";

	private string? QualitySourceName { get; }

	private string? ResolutionHumanReadable
		=> Resolution?.ToString()
			.Replace("R", "")
			.Replace("_", "")
			.ToLower();

	public QualityResolutionModel(QualitySource? source = null, QualityResolution? resolution = null)
	{
		QualitySourceName = source?.GetAttribute<NameAttribute>()?.Name;

		Source = source;
		Resolution = resolution;
	}
}
