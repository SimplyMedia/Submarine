using Submarine.Core.Quality;

namespace Submarine.Core.Entities;

/// <summary>
///     One entry of a quality profile. Items are ordered low to high quality.
/// </summary>
/// <param name="Quality">The quality this entry describes.</param>
/// <param name="Allowed">Whether this quality may be grabbed.</param>
public sealed record QualityProfileItem(QualityResolutionModel Quality, bool Allowed);
