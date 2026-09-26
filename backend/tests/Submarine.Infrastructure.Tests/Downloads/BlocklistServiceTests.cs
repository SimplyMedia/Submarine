using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Submarine.Core.DecisionEngine;
using Submarine.Core.Entities;
using Submarine.Core.Indexers;
using Submarine.Core.Provider;
using Submarine.Core.Release;
using Submarine.Infrastructure.Downloads;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Downloads;

public sealed class BlocklistServiceTests : IDisposable
{
	private readonly SqliteSubmarineDbContext _db = TestDbFactory.Create(new FakeTimeProvider());
	private readonly BlocklistService _service;

	public BlocklistServiceTests() => _service = new BlocklistService(_db);

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task IsBlocklistedAsync_ShouldReturnTrue_ByGuid()
	{
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "X", Guid = "guid-1", Protocol = Protocol.BITTORRENT, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();

		(await _service.IsBlocklistedAsync("Something.Else", "guid-1", null, null, TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	public async Task IsBlocklistedAsync_ShouldReturnTrue_ByInfoHashStoredInGuid()
	{
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "X", Guid = "ABCDEF0123456789ABCDEF0123456789ABCDEF01", Protocol = Protocol.BITTORRENT, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();

		(await _service.IsBlocklistedAsync("Anything", null, null, "ABCDEF0123456789ABCDEF0123456789ABCDEF01", TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	public async Task IsBlocklistedAsync_ShouldReturnTrue_ByTitleAndIndexer()
	{
		var indexer = new Indexer { Name = "Indexer" };
		_db.Indexers.Add(indexer);
		_db.SaveChanges();
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "Show.S01E01.1080p", IndexerId = indexer.Id, Protocol = Protocol.USENET, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();

		(await _service.IsBlocklistedAsync("Show.S01E01.1080p", null, indexer.Id, null, TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	public async Task IsBlocklistedAsync_ShouldReturnFalse_WhenTitleMatchesButIndexerDiffers()
	{
		var indexerA = new Indexer { Name = "Indexer A" };
		var indexerB = new Indexer { Name = "Indexer B" };
		_db.Indexers.AddRange(indexerA, indexerB);
		_db.SaveChanges();
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "Show.S01E01.1080p", IndexerId = indexerA.Id, Protocol = Protocol.USENET, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();

		(await _service.IsBlocklistedAsync("Show.S01E01.1080p", null, indexerB.Id, null, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	public async Task IsBlocklistedAsync_ShouldReturnFalse_WhenNothingMatches()
		=> (await _service.IsBlocklistedAsync("Nothing", "no-guid", null, null, TestContext.Current.CancellationToken)).ShouldBeFalse();

	[Fact]
	public async Task BuildPredicateAsync_ShouldMatchCandidate_ByGuid()
	{
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "X", Guid = "guid-9", Protocol = Protocol.BITTORRENT, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();
		var predicate = await _service.BuildPredicateAsync(TestContext.Current.CancellationToken);

		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "guid-9", Title = "Unrelated Title" });

		predicate(candidate).ShouldBeTrue();
	}

	[Fact]
	public async Task BuildPredicateAsync_ShouldMatchCandidate_ByInfoHash()
	{
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "X", Guid = "HASH123", Protocol = Protocol.BITTORRENT, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();
		var predicate = await _service.BuildPredicateAsync(TestContext.Current.CancellationToken);

		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "different-guid", InfoHash = "HASH123", Title = "Unrelated Title" });

		predicate(candidate).ShouldBeTrue();
	}

	[Fact]
	public async Task BuildPredicateAsync_ShouldMatchCandidate_ByTitleAndIndexer()
	{
		var indexer = new Indexer { Name = "Indexer" };
		_db.Indexers.Add(indexer);
		_db.SaveChanges();
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "Show.S01E01.1080p", IndexerId = indexer.Id, Protocol = Protocol.USENET, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();
		var predicate = await _service.BuildPredicateAsync(TestContext.Current.CancellationToken);

		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "some-guid", Title = "Show.S01E01.1080p", IndexerId = indexer.Id });

		predicate(candidate).ShouldBeTrue();
	}

	[Fact]
	public async Task BuildPredicateAsync_ShouldNotMatch_WhenNothingCorresponds()
	{
		var indexer = new Indexer { Name = "Indexer" };
		_db.Indexers.Add(indexer);
		_db.SaveChanges();
		_db.BlocklistItems.Add(new BlocklistItem { ReleaseTitle = "Show.S01E01.1080p", IndexerId = indexer.Id, Protocol = Protocol.USENET, Reason = "test", Date = DateTime.UtcNow });
		_db.SaveChanges();
		var predicate = await _service.BuildPredicateAsync(TestContext.Current.CancellationToken);

		var candidate = new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "unrelated-guid", Title = "Different.Release", IndexerId = indexer.Id });

		predicate(candidate).ShouldBeFalse();
	}


	[Fact]
	public async Task BuildPredicateAsync_ShouldReturnAlwaysFalsePredicate_WhenBlocklistEmpty()
	{
		var predicate = await _service.BuildPredicateAsync(TestContext.Current.CancellationToken);

		predicate(new ReleaseCandidate(new BaseRelease(), new ReleaseInfo { Guid = "x", Title = "y" })).ShouldBeFalse();
	}
}
