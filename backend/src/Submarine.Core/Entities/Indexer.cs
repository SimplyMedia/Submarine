using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;

namespace Submarine.Core.Entities;

/// <summary>
///     A configured indexer instance.
/// </summary>
public sealed class Indexer : Entity
{
	/// <summary>Name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Implementation backing this indexer.</summary>
	public IndexerImplementation Implementation { get; set; }

	/// <summary>Cardigann definition id, when the implementation is CARDIGANN.</summary>
	public string? DefinitionId { get; set; }

	/// <summary>Protocol of the indexer.</summary>
	public Protocol Protocol { get; set; }

	/// <summary>Base URL.</summary>
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>Implementation specific settings as JSON.</summary>
	public string SettingsJson { get; set; } = "{}";

	/// <summary>Include in RSS sync.</summary>
	public bool EnableRss { get; set; } = true;

	/// <summary>Include in automatic searches.</summary>
	public bool EnableAutomaticSearch { get; set; } = true;

	/// <summary>Include in interactive searches.</summary>
	public bool EnableInteractiveSearch { get; set; } = true;

	/// <summary>Query priority, 1 is highest, 50 lowest.</summary>
	public int Priority { get; set; } = 25;

	/// <summary>Download client used for grabs, null for the default.</summary>
	public int? DownloadClientId { get; set; }

	/// <summary>Download client used for grabs.</summary>
	public DownloadClient? DownloadClient { get; set; }

	/// <summary>Proxy used for requests.</summary>
	public int? ProxyId { get; set; }

	/// <summary>Proxy used for requests.</summary>
	public IndexerProxy? Proxy { get; set; }

	/// <summary>Newznab categories searched.</summary>
	public List<int> Categories { get; set; } = [];

	/// <summary>Anime categories searched.</summary>
	public List<int> AnimeCategories { get; set; } = [];

	/// <summary>Minimum seeders for torrent grabs.</summary>
	public int? MinimumSeeders { get; set; }

	/// <summary>Seed ratio goal.</summary>
	public double? SeedRatio { get; set; }

	/// <summary>Seed time goal in minutes.</summary>
	public int? SeedTimeMinutes { get; set; }

	/// <summary>Seed time goal in minutes for season packs.</summary>
	public int? SeasonPackSeedTimeMinutes { get; set; }

	/// <summary>Search standard anime episode formats.</summary>
	public bool AnimeStandardFormatSearch { get; set; }

	/// <summary>VIP expiration date as an ISO 8601 string, when the indexer reports one. Null when not a VIP indexer.</summary>
	public string? VipExpiration { get; set; }

	/// <summary>Maximum queries (search and RSS) allowed per <see cref="LimitsUnit" /> window. Null for no limit.</summary>
	public int? QueryLimit { get; set; }

	/// <summary>Maximum grabs allowed per <see cref="LimitsUnit" /> window. Null for no limit.</summary>
	public int? GrabLimit { get; set; }

	/// <summary>Window unit that <see cref="QueryLimit" /> and <see cref="GrabLimit" /> are measured over.</summary>
	public IndexerLimitsUnit LimitsUnit { get; set; } = IndexerLimitsUnit.DAY;

	/// <summary>
	///     When true, the outbound download proxy redirects the caller straight to the release link instead of
	///     fetching and re-serving it. Required for Usenet indexers.
	/// </summary>
	public bool Redirect { get; set; }

	/// <summary>
	///     Torrent flags at least one of which a release must carry to be grabbed, empty allows any. Ignored for usenet.
	/// </summary>
	public List<IndexerFlag> RequiredFlags { get; set; } = [];

	/// <summary>
	///     Maximum age in days a season may reach, past its last aired episode, before a single-episode season search
	///     result is held back for a season pack instead. 0 disables the check.
	/// </summary>
	public int SeasonSearchMaximumSingleEpisodeAge { get; set; }

	/// <summary>Tags.</summary>
	public ICollection<Tag> Tags { get; set; } = [];
}
