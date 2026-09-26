using Shouldly;
using Xunit;
using Submarine.Api.Features.Calendar;

namespace Submarine.Api.Tests.Library;

/// <summary>
///     Calendar range normalization: defaults, swapping and the 90 day cap.
/// </summary>
public sealed class CalendarRangeTests
{
	[Fact]
	public void NormalizeRange_ShouldDefaultToTodayPlus7Days()
	{
		var (from, to) = CalendarModule.NormalizeRange(null, null);
		from.ShouldBe(DateTime.UtcNow.Date, TimeSpan.FromSeconds(1));
		to.ShouldBe(DateTime.UtcNow.Date.AddDays(7), TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void NormalizeRange_ShouldSwapInvertedRanges()
	{
		var start = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
		var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
		var (from, to) = CalendarModule.NormalizeRange(start, end);
		from.ShouldBe(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
		to.ShouldBe(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void NormalizeRange_ShouldCapAt90Days()
	{
		var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var end = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
		var (from, to) = CalendarModule.NormalizeRange(start, end);
		from.ShouldBe(start);
		to.ShouldBe(start.AddDays(90));
	}
}
