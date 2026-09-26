using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.IndexerManagement;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class IndexerStatusServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly Submarine.Infrastructure.Persistence.SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly IndexerStatusService _service;

	public IndexerStatusServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		_db.Indexers.Add(new Indexer { Id = 1, Name = "Stub", BaseUrl = "http://x" });
		_db.SaveChanges();
		_service = new IndexerStatusService(_db, _clock, _eventBus);
	}

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task IsAvailableAsync_ShouldBeTrue_WhenNoStatusRecorded()
		=> (await _service.IsAvailableAsync(1)).ShouldBeTrue();

	[Fact]
	public async Task RecordFailureAsync_ShouldDisableFor5Minutes_OnFirstFailure()
	{
		await _service.RecordFailureAsync(1);

		var status = _db.IndexerStatuses.Single(status => status.IndexerId == 1);
		status.DisabledUntil.ShouldBe(_clock.GetUtcNow().UtcDateTime.AddMinutes(5));
		status.EscalationLevel.ShouldBe(1);
		(await _service.IsAvailableAsync(1)).ShouldBeFalse();
	}

	[Theory]
	[InlineData(2, 15)]
	[InlineData(3, 30)]
	[InlineData(4, 60)]
	[InlineData(5, 180)]
	[InlineData(6, 360)]
	[InlineData(7, 720)]
	[InlineData(8, 1440)]
	[InlineData(9, 1440)]
	[InlineData(10, 1440)]
	public async Task RecordFailureAsync_ShouldEscalateBackoffWindow_OnRepeatedFailures(int failureCount, int expectedMinutes)
	{
		for (var i = 0; i < failureCount; i++)
		{
			await _service.RecordFailureAsync(1);
		}

		var status = _db.IndexerStatuses.Single(status => status.IndexerId == 1);
		status.DisabledUntil.ShouldBe(_clock.GetUtcNow().UtcDateTime.AddMinutes(expectedMinutes));
	}

	[Fact]
	public async Task RecordSuccessAsync_ShouldClearBackoffAndEscalationLevel()
	{
		await _service.RecordFailureAsync(1);
		await _service.RecordFailureAsync(1);

		await _service.RecordSuccessAsync(1);

		var status = _db.IndexerStatuses.Single(status => status.IndexerId == 1);
		status.DisabledUntil.ShouldBeNull();
		status.EscalationLevel.ShouldBe(0);
		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}

	[Fact]
	public async Task IsAvailableAsync_ShouldBecomeTrueAgain_OnceBackoffWindowElapses()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(6));

		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}
}
