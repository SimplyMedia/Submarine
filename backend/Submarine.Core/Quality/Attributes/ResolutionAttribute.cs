using System;

namespace Submarine.Core.Quality.Attributes;

/// <summary>
///     Attribute to specify the Resolution a Quality is available in
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class ResolutionAttribute : Attribute
{
	/// <summary>
	///     The Resolutions this Quality is available in
	/// </summary>
	public QualityResolution[] Resolutions { get; }

	/// <summary>
	///     Creates a new instance of <see cref="ResolutionAttribute" />
	/// </summary>
	/// <param name="resolutions">The Resolutions this Quality is available in</param>
	public ResolutionAttribute(params QualityResolution[] resolutions)
		=> Resolutions = resolutions;
}
