using Submarine.Core.Entities;
using Submarine.Core.Quality;

namespace Submarine.Core.Profiles;

/// <summary>
///     Sonarr like quality profile presets. Items are ordered low to high: by resolution, then by source quality.
/// </summary>
public static class QualityProfileTemplates
{
	private static readonly QualityResolutionModel[] Ordered = QualityResolutionModel.All
		.OrderBy(quality => ResolutionTier(quality.Resolution))
		.ThenBy(quality => SourceRank(quality.Source))
		.ToArray();

	/// <summary>
	///     Names of all available templates.
	/// </summary>
	public static IReadOnlyList<string> Names { get; } =
	[
		"Any",
		"SD",
		"HD-720p",
		"HD-1080p",
		"Ultra-HD",
		"HD - 720p/1080p",
		"Remux-1080p",
		"Remux-2160p"
	];

	/// <summary>
	///     Creates a quality profile from the named template.
	/// </summary>
	/// <param name="name">Template name, matched case-insensitively.</param>
	/// <exception cref="KeyNotFoundException">The template name is unknown.</exception>
	public static QualityProfile Create(string name)
	{
		var allowed = ResolveAllowed(name);
		var items = Ordered.Select(quality => new QualityProfileItem(quality, allowed(quality))).ToList();
		var cutoff = items.FindLastIndex(item => item.Allowed);

		return new QualityProfile
		{
			Name = name,
			UpgradeAllowed = true,
			Cutoff = cutoff < 0 ? 0 : cutoff,
			Items = items
		};
	}

	private static Func<QualityResolutionModel, bool> ResolveAllowed(string name)
	{
		switch (name.ToLowerInvariant())
		{
			case "any":
				return _ => true;
			case "sd":
				return quality => ResolutionTier(quality.Resolution) is >= 0 and <= 3;
			case "hd-720p":
				return ByResolution(QualityResolution.R720_P);
			case "hd-1080p":
				return ByResolution(QualityResolution.R1080_P);
			case "ultra-hd":
				return ByResolution(QualityResolution.R2160_P);
			case "hd - 720p/1080p":
				return quality => quality.Resolution is QualityResolution.R720_P or QualityResolution.R1080_P;
			case "remux-1080p":
				return Remux(QualityResolution.R1080_P);
			case "remux-2160p":
				return Remux(QualityResolution.R2160_P);
			default:
				throw new KeyNotFoundException($"Unknown quality profile template '{name}'");
		}
	}

	private static Func<QualityResolutionModel, bool> ByResolution(QualityResolution resolution)
		=> quality => quality.Resolution == resolution;

	private static Func<QualityResolutionModel, bool> Remux(QualityResolution resolution)
		=> quality => quality.Resolution == resolution
		              && quality.Source is QualitySource.WEB_DL or QualitySource.BLURAY or QualitySource.BLURAY_REMUX;

	private static int ResolutionTier(QualityResolution? resolution) => resolution switch
	{
		QualityResolution.R360_P => 0,
		QualityResolution.R480_P => 1,
		QualityResolution.R540_P => 2,
		QualityResolution.R576_P => 3,
		QualityResolution.R720_P => 4,
		QualityResolution.R1080_P => 5,
		QualityResolution.R2160_P => 6,
		_ => -1
	};

	private static int SourceRank(QualitySource? source) => source switch
	{
		QualitySource.UNKNOWN => 0,
		QualitySource.CAM => 1,
		QualitySource.TV => 2,
		QualitySource.DVD => 3,
		QualitySource.RAW_HD => 4,
		QualitySource.WEB_RIP => 5,
		QualitySource.WEB_DL => 6,
		QualitySource.BLURAY => 7,
		QualitySource.BLURAY_REMUX => 8,
		QualitySource.BLURAY_DISK => 9,
		_ => -1
	};
}
