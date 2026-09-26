using Shouldly;
using Submarine.Infrastructure.Commands;
using Xunit;

namespace Submarine.Api.Tests.Commands;

public sealed class ScheduleCalculatorTests
{
	[Fact]
	public void ComputeNextRun_ShouldBeDueImmediately_WhenNeverRan()
	{
		var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

		ScheduleCalculator.ComputeNextRun(null, 30, now).ShouldBe(now);
	}

	[Fact]
	public void ComputeNextRun_ShouldAddIntervalToLastRun()
	{
		var lastRun = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		var now = lastRun.AddMinutes(5);

		ScheduleCalculator.ComputeNextRun(lastRun, 30, now).ShouldBe(lastRun.AddMinutes(30));
	}

	[Fact]
	public void ComputeNextRun_ShouldBeInThePast_WhenRestartHappenedLate()
	{
		var lastRun = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		var now = lastRun.AddHours(3);

		var next = ScheduleCalculator.ComputeNextRun(lastRun, 30, now);

		(next < now).ShouldBeTrue();
	}
}
