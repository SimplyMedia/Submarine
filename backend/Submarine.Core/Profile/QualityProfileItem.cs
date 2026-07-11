using Submarine.Core.Quality;

namespace Submarine.Core.Profile;

/// <summary>
///     An entry in a <see cref="QualityProfile" />'s ordered list of Qualities
/// </summary>
public record QualityProfileItem
{
	/// <summary>
	///     The Quality this item represents
	/// </summary>
	public QualityResolutionModel Quality { get; init; }

	/// <summary>
	///     Whether this Quality is allowed to be downloaded
	/// </summary>
	public bool Allowed { get; init; }
}
