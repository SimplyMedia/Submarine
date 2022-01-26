namespace Submarine.Core.Provider;

/// <summary>
///     A Bittorrent Provider (Tracker) is a Provider which serves files via the <see cref="Protocol.BITTORRENT" />
///     <see cref="Protocol" />
/// </summary>
public class BittorrentProvider : Provider
{
	/// <summary>
	///     Minimum Seeders for Releases of this <see cref="BittorrentProvider" /> to be considered a downloadable Release
	/// </summary>
	public int? MinimumSeeders { get; set; }

	/// <summary>
	///     Seed Ratio after which Releases of this <see cref="BittorrentProvider" /> will be removed from your Download Client
	/// </summary>
	public float? SeedRatio { get; set; }

	/// <summary>
	///     Seed Time after which Releases of this <see cref="BittorrentProvider" /> will be removed from your Download Client
	/// </summary>
	public long? SeedTime { get; set; }

	/// <summary>
	///     Seed Time after which Season Pack Releases of this <see cref="BittorrentProvider" /> will be removed from your
	///     Download Client
	/// </summary>
	public long? SeasonPackSeedTime { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="BittorrentProvider" />
	/// </summary>
	public BittorrentProvider()
		=> Protocol = Protocol.BITTORRENT;
}
