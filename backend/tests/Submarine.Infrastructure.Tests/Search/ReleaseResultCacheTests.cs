using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Caching.Memory;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Indexers;
using Submarine.Core.Parser.Release;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.Search;
using Shouldly;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class ReleaseResultCacheTests
{
	[Fact]
	public void TryGet_ShouldExpireAfterTenMinutes()
	{
		var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
#pragma warning disable CS0618
		using var memoryCache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
#pragma warning restore CS0618
		var cache = new ReleaseResultCache(memoryCache);
		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "ttl", IndexerId = 17 });
		cache.Store(candidate);

		clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(1));
		cache.TryGet("ttl", 17, out _).ShouldBeTrue();
		clock.Advance(TimeSpan.FromTicks(1));
		cache.TryGet("ttl", 17, out _).ShouldBeFalse();
	}

	private sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
	{
		public DateTimeOffset UtcNow { get; private set; } = utcNow;

		public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
	}
	
	[Fact]
	public void TryGet_ShouldReturnStoredCandidateOnlyForSameGuidAndIndexer()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var cache = new ReleaseResultCache(memoryCache);
		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo
		{
			Guid = "release-guid",
			IndexerId = 17,
			Protocol = Protocol.BITTORRENT
		});

		cache.Store(candidate);

		cache.TryGet("release-guid", 17, out var found).ShouldBeTrue();
		found.ShouldBeSameAs(candidate);
		cache.TryGet("release-guid", 18, out _).ShouldBeFalse();
		cache.TryGet("different-guid", 17, out _).ShouldBeFalse();
	}

	[Fact]
	public void Store_ShouldIgnoreCandidatesWithoutIndexerOrGuid()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var cache = new ReleaseResultCache(memoryCache);
		cache.Store(new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "release-guid" }));
		cache.Store(new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { IndexerId = 17 }));

		cache.TryGet("release-guid", 17, out _).ShouldBeFalse();
	}
}
