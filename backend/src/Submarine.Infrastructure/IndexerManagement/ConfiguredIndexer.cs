using Submarine.Core.Entities;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     Which enable flag of an <see cref="Indexer" /> gates its participation in a search.
/// </summary>
public enum IndexerSearchMode
{
	/// <summary>RSS sync.</summary>
	RSS,

	/// <summary>Automatic search, triggered by commands.</summary>
	AUTOMATIC,

	/// <summary>Interactive search, triggered by a user.</summary>
	INTERACTIVE
}

/// <summary>
///     A live <see cref="IIndexer" /> instance paired with the configuration row it was built from. Dispose the
///     client once done with it.
/// </summary>
/// <param name="Entity">The configured indexer row.</param>
/// <param name="Client">The live indexer client.</param>
public sealed record ConfiguredIndexer(Indexer Entity, IIndexer Client);
