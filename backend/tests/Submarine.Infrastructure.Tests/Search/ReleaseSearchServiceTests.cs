using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Events;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Search;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class ReleaseSearchServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IIndexerProvider _indexerProvider = Substitute.For<IIndexerProvider>();
	private readonly ReleaseSearchService _service;

	public ReleaseSearchServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		var statusService = new IndexerStatusService(_db, _clock, Substitute.For<IEventBus>());
		var historyRecorder = new IndexerHistoryRecorder(_db, _clock);
		_service = new ReleaseSearchService(_indexerProvider, statusService, historyRecorder, NullLogger<ReleaseSearchService>.Instance);
	}

	public void Dispose() => _db.Dispose();

	private static IIndexer FakeIndexer(int delayMs, IReadOnlyList<ReleaseInfo> releases, bool fail = false)
	{
		var indexer = Substitute.For<IIndexer>();
		indexer.FetchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>()).Returns(async callInfo =>
		{
			await Task.Delay(delayMs, callInfo.Arg<CancellationToken>());
			if (fail)
			{
				throw new HttpRequestException("boom");
			}

			return releases;
		});
		return indexer;
	}

	[Fact]
	public async Task SearchAsync_ShouldNotThrow_AndShouldRecordEveryIndexer_WhenIndexersFetchConcurrently()
	{
		var one = new Indexer { Id = 1, Name = "One", BaseUrl = "http://one" };
		var two = new Indexer { Id = 2, Name = "Two", BaseUrl = "http://two" };
		_db.Indexers.AddRange(one, two);
		await _db.SaveChangesAsync();

		// indexer One resolves last so, without the C-F02 fix, its status/history write would race the fast indexer's
		var slowFirst = FakeIndexer(40, [new ReleaseInfo { Guid = "g1", Title = "One.Release" }]);
		var fastSecond = FakeIndexer(0, [], fail: true);

		_indexerProvider.GetEnabledAsync(IndexerSearchMode.AUTOMATIC, Arg.Any<CancellationToken>())
			.Returns([new ConfiguredIndexer(one, slowFirst), new ConfiguredIndexer(two, fastSecond)]);

		var results = await _service.SearchAsync(new BasicSearchRequest("query"), IndexerSearchMode.AUTOMATIC, "test", TestContext.Current.CancellationToken);

		results.Count.ShouldBe(1);
		results[0].Guid.ShouldBe("g1");

		var histories = await _db.IndexerHistories.ToListAsync(TestContext.Current.CancellationToken);
		histories.Count.ShouldBe(2);
		histories.ShouldContain(entry => entry.IndexerId == 1 && entry.Successful);
		histories.ShouldContain(entry => entry.IndexerId == 2 && !entry.Successful && entry.EventType == IndexerHistoryEventType.FAILED);

		var statuses = await _db.IndexerStatuses.ToListAsync(TestContext.Current.CancellationToken);
		statuses.ShouldContain(status => status.IndexerId == 2 && status.EscalationLevel > 0);
	}
}
