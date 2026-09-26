using Submarine.Core.Quality;

namespace Submarine.Api.Features.Compat.Shared;

/// <summary>Explicit Sonarr/Radarr quality identifiers; never derived from native enum order.</summary>
public static class CompatQualityMap
{
	private static readonly IReadOnlyDictionary<(string Facade, QualitySource Source, QualityResolution? Resolution), int> Ids = BuildIds();
	private static readonly IReadOnlyDictionary<(string Facade, int Id), (QualitySource Source, QualityResolution? Resolution)> Qualities = BuildQualities();

	public static int? ToUpstreamQualityId(QualityResolutionModel quality, string facade)
	{
		if (!IsFacade(facade) || quality.Source is not { } source)
			return null;
		return Ids.TryGetValue((facade, source, quality.Resolution), out var id) ? id : null;
	}

	public static bool TryGetNativeQuality(int upstreamId, string facade, out QualityResolutionModel quality)
	{
		if (IsFacade(facade) && Qualities.TryGetValue((facade, upstreamId), out var mapped))
		{
			quality = new QualityResolutionModel(mapped.Source, mapped.Resolution);
			return true;
		}

		quality = null!;
		return false;
	}

	private static bool IsFacade(string facade) => facade is "sonarr" or "radarr";

	private static Dictionary<(string Facade, QualitySource Source, QualityResolution? Resolution), int> BuildIds()
	{
		var map = new Dictionary<(string, QualitySource, QualityResolution?), int>();
		AddBoth(QualitySource.UNKNOWN, null, 0);
		AddBoth(QualitySource.DVD, null, 2);
		AddBoth(QualitySource.RAW_HD, null, 10);
		Add("sonarr", QualitySource.CAM, null, null);
		Add("radarr", QualitySource.CAM, null, 25);
		AddBoth(QualitySource.TV, QualityResolution.R480_P, 1);
		AddBoth(QualitySource.TV, QualityResolution.R720_P, 4);
		AddBoth(QualitySource.TV, QualityResolution.R1080_P, 9);
		AddBoth(QualitySource.TV, QualityResolution.R2160_P, 16);
		AddBoth(QualitySource.WEB_DL, QualityResolution.R480_P, 8);
		AddBoth(QualitySource.WEB_DL, QualityResolution.R720_P, 5);
		AddBoth(QualitySource.WEB_DL, QualityResolution.R1080_P, 3);
		AddBoth(QualitySource.WEB_DL, QualityResolution.R2160_P, 18);
		AddBoth(QualitySource.WEB_RIP, QualityResolution.R480_P, 12);
		AddBoth(QualitySource.WEB_RIP, QualityResolution.R720_P, 14);
		AddBoth(QualitySource.WEB_RIP, QualityResolution.R1080_P, 15);
		AddBoth(QualitySource.WEB_RIP, QualityResolution.R2160_P, 17);
		Add("sonarr", QualitySource.BLURAY, QualityResolution.R480_P, 13);
		Add("radarr", QualitySource.BLURAY, QualityResolution.R480_P, 20);
		Add("sonarr", QualitySource.BLURAY, QualityResolution.R576_P, 22);
		Add("radarr", QualitySource.BLURAY, QualityResolution.R576_P, 21);
		AddBoth(QualitySource.BLURAY, QualityResolution.R720_P, 6);
		AddBoth(QualitySource.BLURAY, QualityResolution.R1080_P, 7);
		AddBoth(QualitySource.BLURAY, QualityResolution.R2160_P, 19);
		Add("sonarr", QualitySource.BLURAY_REMUX, QualityResolution.R1080_P, 20);
		Add("sonarr", QualitySource.BLURAY_REMUX, QualityResolution.R2160_P, 21);
		Add("radarr", QualitySource.BLURAY_REMUX, QualityResolution.R1080_P, 30);
		Add("radarr", QualitySource.BLURAY_REMUX, QualityResolution.R2160_P, 31);
		Add("radarr", QualitySource.BLURAY_DISK, QualityResolution.R720_P, 22);
		Add("radarr", QualitySource.BLURAY_DISK, QualityResolution.R1080_P, 22);
		Add("radarr", QualitySource.BLURAY_DISK, QualityResolution.R2160_P, 22);
		return map;

		void AddBoth(QualitySource source, QualityResolution? resolution, int id)
		{
			Add("sonarr", source, resolution, id);
			Add("radarr", source, resolution, id);
		}

		void Add(string facade, QualitySource source, QualityResolution? resolution, int? id)
		{
			if (id is { } value)
				map.Add((facade, source, resolution), value);
		}
	}

	private static Dictionary<(string Facade, int Id), (QualitySource Source, QualityResolution? Resolution)> BuildQualities()
		=> Ids
			.GroupBy(x => (x.Key.Facade, x.Value))
			.Where(group => group.Count() == 1)
			.ToDictionary(group => group.Key, group => (group.Single().Key.Source, group.Single().Key.Resolution));
}
