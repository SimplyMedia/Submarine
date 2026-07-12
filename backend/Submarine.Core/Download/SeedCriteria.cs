namespace Submarine.Core.Download;

/// <summary>
///     Seeding limits a torrent download client should apply to a download
/// </summary>
/// <param name="Ratio">The seed ratio after which the download may be stopped</param>
/// <param name="SeedTimeMinutes">The seed time in minutes after which the download may be stopped</param>
/// <param name="SeasonPackSeedTimeMinutes">The seed time in minutes for season packs after which the download may be stopped</param>
public record SeedCriteria(double? Ratio, int? SeedTimeMinutes, int? SeasonPackSeedTimeMinutes);
