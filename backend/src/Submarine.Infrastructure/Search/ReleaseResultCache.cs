using Microsoft.Extensions.Caching.Memory;
using Submarine.Core.DecisionEngine;

namespace Submarine.Infrastructure.Search;

/// <summary>
///     Holds the candidates of the last interactive searches for ten minutes so a subsequent grab does not need to
///     search again. Keyed by release guid and indexer id.
/// </summary>
/// <param name="cache">The backing memory cache.</param>
public sealed class ReleaseResultCache(IMemoryCache cache)
{
	private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

	/// <summary>
	///     Stores a candidate under its release guid and indexer id.
	/// </summary>
	public void Store(ReleaseCandidate candidate)
	{
		if (candidate.Info.IndexerId is not { } indexerId || string.IsNullOrEmpty(candidate.Info.Guid))
		{
			return;
		}

		cache.Set(Key(candidate.Info.Guid, indexerId), candidate, Ttl);
	}

	/// <summary>
	///     Looks up a previously stored candidate.
	/// </summary>
	public bool TryGet(string guid, int indexerId, out ReleaseCandidate candidate)
	{
		if (cache.TryGetValue(Key(guid, indexerId), out ReleaseCandidate? value) && value is not null)
		{
			candidate = value;
			return true;
		}

		candidate = null!;
		return false;
	}

	private static string Key(string guid, int indexerId)
		=> $"release:{indexerId}:{guid}";
}
