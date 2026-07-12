using Submarine.Core.Library;

namespace Submarine.Api.Models.Response;

/// <summary>
///     A <see cref="MediaVersion" /> of a Series or Movie
/// </summary>
public record MediaVersionResponse(
	int Id,
	string Name,
	int QualityProfileId,
	int LanguageProfileId,
	string Path,
	bool Monitored)
{
	public static MediaVersionResponse FromVersion(MediaVersion version)
		=> new(version.Id, version.Name, version.QualityProfileId, version.LanguageProfileId, version.Path,
			version.Monitored);
}
