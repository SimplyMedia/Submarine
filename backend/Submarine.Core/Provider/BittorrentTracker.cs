namespace Submarine.Core.Provider;

/// <summary>
///     A Bittorrent Tracker is a Provider which serves files via <see cref="Protocol.BITTORRENT" />
/// </summary>
public class BittorrentTracker : Provider
{
	/// <summary>
	///     Minimum Seeders for Releases of this <see cref="BittorrentTracker" /> to be considered a downloadable Release
	/// </summary>
	public int? MinimumSeeders { get; set; }

	/// <summary>
	///     Seed Ratio after which Releases of this <see cref="BittorrentTracker" /> will be removed from your Download Client
	/// </summary>
	public float? SeedRatio { get; set; }

	/// <summary>
	///     Seed Time after which Releases of this <see cref="BittorrentTracker" /> will be removed from your Download Client
	/// </summary>
	public long? SeedTime { get; set; }

	/// <summary>
	///     Seed Time after which Season Pack Releases of this <see cref="BittorrentTracker" /> will be removed from your
	///     Download Client
	/// </summary>
	public long? SeasonPackSeedTime { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="BittorrentTracker" />
	/// </summary>
	public BittorrentTracker()
		=> Protocol = Protocol.BITTORRENT;
}
