using Submarine.Core.Provider;

namespace Submarine.Core.Enums;

/// <summary>
///     Access type of an indexer definition.
/// </summary>
public enum IndexerDefinitionType
{
	/// <summary>Public indexer.</summary>
	PUBLIC,

	/// <summary>Private indexer.</summary>
	PRIVATE,

	/// <summary>Semi-private indexer.</summary>
	SEMI_PRIVATE
}

/// <summary>
///     Proxy type used for indexer requests.
/// </summary>
public enum IndexerProxyType
{
	/// <summary>HTTP proxy.</summary>
	HTTP,

	/// <summary>Socks4 proxy.</summary>
	SOCKS4,

	/// <summary>Socks5 proxy.</summary>
	SOCKS5,

	/// <summary>FlareSolverr instance.</summary>
	FLARESOLVERR
}

/// <summary>
///     Type of an indexer history entry.
/// </summary>
public enum IndexerHistoryEventType
{
	/// <summary>Interactive or automatic search query.</summary>
	QUERY,

	/// <summary>RSS sync query.</summary>
	RSS,

	/// <summary>Release grab.</summary>
	GRAB,

	/// <summary>Authentication.</summary>
	AUTH,

	/// <summary>Failure.</summary>
	FAILED
}

/// <summary>
///     Interval unit for a per-indexer query or grab limit.
/// </summary>
public enum IndexerLimitsUnit
{
	/// <summary>24 hour window.</summary>
	DAY,

	/// <summary>1 hour window.</summary>
	HOUR
}
