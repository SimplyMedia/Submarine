using System.Collections.Generic;

namespace Submarine.Core.Provider;

/// <summary>
///     A Torznab Indexer is a <see cref="Protocol.BITTORRENT" /> Provider speaking the Torznab API
/// </summary>
public class TorznabIndexer : Provider
{
	/// <summary>
	///     Category ids queried for standard Releases
	/// </summary>
	public List<int> Categories { get; set; } = new();

	/// <summary>
	///     Category ids queried for Anime Releases
	/// </summary>
	public List<int> AnimeCategories { get; set; } = new();

	/// <summary>
	///     Minimum Seeders for Releases of this Indexer to be considered a downloadable Release
	/// </summary>
	public int MinimumSeeders { get; set; } = 1;

	/// <summary>
	///     Seed Ratio after which Releases of this Indexer will be removed from your Download Client
	/// </summary>
	public float? SeedRatio { get; set; }

	/// <summary>
	///     Seed Time after which Releases of this Indexer will be removed from your Download Client
	/// </summary>
	public long? SeedTime { get; set; }

	/// <summary>
	///     Seed Time after which Season Pack Releases of this Indexer will be removed from your Download Client
	/// </summary>
	public long? SeasonPackSeedTime { get; set; }

	/// <summary>
	///     Creates a new instance of <see cref="TorznabIndexer" />
	/// </summary>
	public TorznabIndexer()
		=> Protocol = Protocol.BITTORRENT;
}
