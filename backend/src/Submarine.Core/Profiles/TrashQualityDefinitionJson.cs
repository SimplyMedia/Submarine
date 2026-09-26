using Submarine.Core.Quality;

namespace Submarine.Core.Profiles;

/// <summary>
///     One TRaSH quality-size entry: a quality name and its per minute size limits in MB.
/// </summary>
/// <param name="Quality">TRaSH quality name, for example "Bluray-1080p" or "Remux-1080p".</param>
/// <param name="Min">Minimum size in MB per minute.</param>
/// <param name="Preferred">Preferred size in MB per minute, sentinel value means unlimited.</param>
/// <param name="Max">Maximum size in MB per minute, sentinel value means unlimited.</param>
public sealed record TrashQualityDefinition(string Quality, double Min, double Preferred, double Max);

/// <summary>
///     Maps TRaSH Guides quality-size JSON (docs/json/{sonarr,radarr}/quality-size/*.json in the TRaSH-Guides repo) to
///     <see cref="Submarine.Core.Entities.QualityDefinition" /> size limits. TRaSH names its qualities differently from
///     Submarine's own <see cref="QualityResolutionModel.Name" />, so entries are matched by an explicit table instead
///     of a fuzzy title match.
/// </summary>
public static class TrashQualityDefinitionJson
{
	// TRaSH publishes series/anime quality-size files with an unlimited sentinel of preferred 995 / max 1000 MB per
	// minute, and movie files with a sentinel of preferred 1999 / max 2000 MB per minute.
	private const double SeriesUnlimitedMax = 1000;
	private const double MovieUnlimitedMax = 2000;

	private static readonly IReadOnlyDictionary<string, (QualitySource Source, QualityResolution? Resolution)> QualityMap =
		new Dictionary<string, (QualitySource, QualityResolution?)>(StringComparer.OrdinalIgnoreCase)
		{
			["SDTV"] = (QualitySource.TV, QualityResolution.R480_P),
			["DVD"] = (QualitySource.DVD, null),
			["HDTV-720p"] = (QualitySource.TV, QualityResolution.R720_P),
			["HDTV-1080p"] = (QualitySource.TV, QualityResolution.R1080_P),
			["HDTV-2160p"] = (QualitySource.TV, QualityResolution.R2160_P),
			["WEBRip-480p"] = (QualitySource.WEB_RIP, QualityResolution.R480_P),
			["WEBRip-720p"] = (QualitySource.WEB_RIP, QualityResolution.R720_P),
			["WEBRip-1080p"] = (QualitySource.WEB_RIP, QualityResolution.R1080_P),
			["WEBRip-2160p"] = (QualitySource.WEB_RIP, QualityResolution.R2160_P),
			["WEBDL-480p"] = (QualitySource.WEB_DL, QualityResolution.R480_P),
			["WEBDL-720p"] = (QualitySource.WEB_DL, QualityResolution.R720_P),
			["WEBDL-1080p"] = (QualitySource.WEB_DL, QualityResolution.R1080_P),
			["WEBDL-2160p"] = (QualitySource.WEB_DL, QualityResolution.R2160_P),
			["Bluray-480p"] = (QualitySource.BLURAY, QualityResolution.R480_P),
			["Bluray-576p"] = (QualitySource.BLURAY, QualityResolution.R576_P),
			["Bluray-720p"] = (QualitySource.BLURAY, QualityResolution.R720_P),
			["Bluray-1080p"] = (QualitySource.BLURAY, QualityResolution.R1080_P),
			["Bluray-2160p"] = (QualitySource.BLURAY, QualityResolution.R2160_P),
			["Bluray-1080p Remux"] = (QualitySource.BLURAY_REMUX, QualityResolution.R1080_P),
			["Bluray-2160p Remux"] = (QualitySource.BLURAY_REMUX, QualityResolution.R2160_P),
			["Remux-1080p"] = (QualitySource.BLURAY_REMUX, QualityResolution.R1080_P),
			["Remux-2160p"] = (QualitySource.BLURAY_REMUX, QualityResolution.R2160_P)
		};

	/// <summary>
	///     Resolves a TRaSH quality name to the source and resolution of the matching Submarine quality, and its size
	///     limits with the unlimited sentinel converted to null. Returns null when the quality name is not recognised.
	/// </summary>
	public static (QualitySource Source, QualityResolution? Resolution, double? Min, double? Max, double? Preferred)? Resolve(
		TrashQualityDefinition entry)
	{
		if (!QualityMap.TryGetValue(entry.Quality, out var quality))
		{
			return null;
		}

		var unlimitedMax = entry.Max >= MovieUnlimitedMax ? MovieUnlimitedMax : SeriesUnlimitedMax;

		double? min = entry.Min > 0 ? entry.Min : null;
		double? max = entry.Max >= unlimitedMax ? null : entry.Max;
		double? preferred = entry.Preferred >= unlimitedMax - 5 ? null : entry.Preferred;

		return (quality.Source, quality.Resolution, min, max, preferred);
	}
}
