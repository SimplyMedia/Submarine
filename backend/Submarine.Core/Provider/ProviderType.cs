namespace Submarine.Core.Provider;

/// <summary>
///     Concrete <see cref="Provider" /> subtype, used to select which subtype to construct
/// </summary>
public enum ProviderType
{
	/// <summary>
	///     A <see cref="BittorrentTracker" />
	/// </summary>
	BITTORRENT_TRACKER,

	/// <summary>
	///     A <see cref="UsenetIndexer" />
	/// </summary>
	USENET_INDEXER,

	/// <summary>
	///     A <see cref="TorznabIndexer" />
	/// </summary>
	TORZNAB_INDEXER,

	/// <summary>
	///     A <see cref="NewznabIndexer" />
	/// </summary>
	NEWZNAB_INDEXER
}
