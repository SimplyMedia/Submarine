using System;
using System.Linq;
using Submarine.Core.Quality.Attributes;
using Submarine.Core.Util.Extensions;

namespace Submarine.Core.Quality;

/// <summary>
///     A QualityResolutionModel is a combination of a QualitySource and a QualityResolution
/// </summary>
public class QualityResolutionModel
{
	public static QualityResolutionModel[] All { get; } = Enum.GetValues<QualitySource>()
		.Select(s =>
			s.GetAttribute<ResolutionAttribute>()?.Resolutions
				.Select(r => new QualityResolutionModel(s, r))
			?? new[] { new QualityResolutionModel(s) })
		.SelectMany(i => i)
		.ToArray();

	/// <summary>
	///     The Source of this QualityResolutionModel
	/// </summary>
	public QualitySource? Source { get; set; }

	/// <summary>
	///     The Resolution of this QualityResolutionModel
	/// </summary>
	public QualityResolution? Resolution { get; set; }

	/// <summary>
	///     The Name of this QualityResolutionModel, using the Display Name if possible
	/// </summary>
	public string Name
		=> $"{QualitySourceName ?? Source.ToString()}{(Resolution != null ? $"-{ResolutionHumanReadable}" : "")}";

	/// <summary>
	///     The Display Name of the QualitySource, if existing
	/// </summary>
	private string? QualitySourceName { get; }

	/// <summary>
	///     The Resolution of this QualityResolutionModel, in a human readable format
	/// </summary>
	private string? ResolutionHumanReadable
		=> Resolution?.ToString()
			.Replace("R", "")
			.Replace("_", "")
			.ToLower();


	/// <summary>
	///     Create a new QualityResolutionModel
	/// </summary>
	/// <param name="source">The source</param>
	/// <param name="resolution">The resolution</param>
	public QualityResolutionModel(QualitySource? source = null, QualityResolution? resolution = null)
	{
		QualitySourceName = source?.GetAttribute<DisplayNameAttribute>()?.Name;

		Source = source;
		Resolution = resolution;
	}
}
