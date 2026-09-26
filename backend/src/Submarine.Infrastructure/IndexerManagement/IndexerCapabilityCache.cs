using System.Collections.Concurrent;
using Submarine.Core.Indexers;

namespace Submarine.Infrastructure.IndexerManagement;

/// <summary>
///     In-memory cache of indexer capabilities, keyed by indexer id and invalidated when the settings hash changes
///     or the indexer is saved.
/// </summary>
public sealed class IndexerCapabilityCache
{
	private readonly ConcurrentDictionary<int, (string Hash, IndexerCapabilities Capabilities)> _entries = new();

	/// <summary>
	///     Returns the cached capabilities when present and the settings hash still matches.
	/// </summary>
	public bool TryGet(int indexerId, string settingsHash, out IndexerCapabilities capabilities)
	{
		if (_entries.TryGetValue(indexerId, out var entry) && entry.Hash == settingsHash)
		{
			capabilities = entry.Capabilities;
			return true;
		}

		capabilities = null!;
		return false;
	}

	/// <summary>
	///     Returns the cached capabilities regardless of whether the settings hash still matches, for display purposes
	///     that must not trigger a live fetch. Returns false when nothing has been cached yet.
	/// </summary>
	public bool TryGetAny(int indexerId, out IndexerCapabilities capabilities)
	{
		if (_entries.TryGetValue(indexerId, out var entry))
		{
			capabilities = entry.Capabilities;
			return true;
		}

		capabilities = null!;
		return false;
	}

	/// <summary>
	///     Stores the capabilities under the given settings hash.
	/// </summary>
	public void Set(int indexerId, string settingsHash, IndexerCapabilities capabilities)
		=> _entries[indexerId] = (settingsHash, capabilities);

	/// <summary>
	///     Drops the cached capabilities of the indexer.
	/// </summary>
	public void Invalidate(int indexerId)
		=> _entries.TryRemove(indexerId, out _);
}
