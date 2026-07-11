namespace Submarine.Core.Quality;

/// <summary>
///     Contains the QualityResolution and Revision of a Quality
/// </summary>
/// <param name="Resolution">The QualityResolution of this QualityModel</param>
/// <param name="Revision">The Revision of this QualityModel</param>
public record QualityModel(QualityResolutionModel Resolution, Revision Revision);
