namespace Submarine.Core.Download;

/// <summary>
///     Seeding limits a torrent download client should apply to a download; clients apply what their
///     API supports and ignore the rest
/// </summary>
/// <param name="Ratio">Seed ratio after which the download may be stopped</param>
/// <param name="SeedTimeMinutes">Seed time in minutes after which the download may be stopped</param>
/// <param name="SeasonPackSeedTimeMinutes">Seed time in minutes for season packs after which the download may be stopped</param>
public record SeedCriteria(double? Ratio, int? SeedTimeMinutes, int? SeasonPackSeedTimeMinutes);
