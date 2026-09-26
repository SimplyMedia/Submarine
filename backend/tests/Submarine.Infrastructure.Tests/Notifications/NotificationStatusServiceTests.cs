using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Events;
using Submarine.Infrastructure.Notifications;
using Submarine.Infrastructure.Persistence;
using Submarine.Infrastructure.Tests.Import;
using Xunit;

namespace Submarine.Infrastructure.Tests.Notifications;

/// <summary>
///     Asserts the grace period, escalating backoff and success reset of <see cref="NotificationStatusService" />.
/// </summary>
public sealed class NotificationStatusServiceTests : IDisposable
{
	private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
	private readonly SqliteSubmarineDbContext _db;
	private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
	private readonly NotificationStatusService _service;

	public NotificationStatusServiceTests()
	{
		_db = TestDbFactory.Create(_clock);
		_db.Notifications.Add(new Notification { Id = 1, Name = "Stub", SettingsJson = "{}" });
		_db.SaveChanges();
		_service = new NotificationStatusService(_db, _clock, _eventBus);
	}

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task IsAvailableAsync_ShouldBeTrue_WhenNoStatusRecorded()
		=> (await _service.IsAvailableAsync(1)).ShouldBeTrue();

	[Fact]
	public async Task RecordFailureAsync_ShouldNotDisable_OnIsolatedFailure()
	{
		await _service.RecordFailureAsync(1);

		var status = _db.NotificationStatuses.Single(status => status.NotificationId == 1);
		status.DisabledUntil.ShouldBeNull();
		status.EscalationLevel.ShouldBe(0);
		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}

	[Fact]
	public async Task RecordFailureAsync_ShouldNotDisable_WhenSecondFailureIsWithinGracePeriod()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(4));
		await _service.RecordFailureAsync(1);

		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}

	[Fact]
	public async Task RecordFailureAsync_ShouldDisableFor1Minute_OnceGracePeriodElapses()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(5));
		await _service.RecordFailureAsync(1);

		var status = _db.NotificationStatuses.Single(status => status.NotificationId == 1);
		status.DisabledUntil.ShouldBe(_clock.GetUtcNow().UtcDateTime.AddMinutes(1));
		status.EscalationLevel.ShouldBe(1);
		(await _service.IsAvailableAsync(1)).ShouldBeFalse();
		await _eventBus.Received().PublishAsync(Arg.Any<NotificationStatusChangedEvent>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RecordFailureAsync_ShouldEscalateBackoffWindow_OnRepeatedFailuresPastGracePeriod()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(5));
		await _service.RecordFailureAsync(1); // escalates to level 1, backoff 1m
		await _service.RecordFailureAsync(1); // escalates to level 2, backoff 5m

		var status = _db.NotificationStatuses.Single(status => status.NotificationId == 1);
		status.DisabledUntil.ShouldBe(_clock.GetUtcNow().UtcDateTime.AddMinutes(5));
		status.EscalationLevel.ShouldBe(2);
	}

	[Fact]
	public async Task RecordSuccessAsync_ShouldClearBackoffAndEscalationLevel()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(5));
		await _service.RecordFailureAsync(1);

		await _service.RecordSuccessAsync(1);

		var status = _db.NotificationStatuses.Single(status => status.NotificationId == 1);
		status.DisabledUntil.ShouldBeNull();
		status.InitialFailure.ShouldBeNull();
		status.EscalationLevel.ShouldBe(0);
		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}

	[Fact]
	public async Task IsAvailableAsync_ShouldBecomeTrueAgain_OnceBackoffWindowElapses()
	{
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(5));
		await _service.RecordFailureAsync(1);
		_clock.Advance(TimeSpan.FromMinutes(2));

		(await _service.IsAvailableAsync(1)).ShouldBeTrue();
	}
}
