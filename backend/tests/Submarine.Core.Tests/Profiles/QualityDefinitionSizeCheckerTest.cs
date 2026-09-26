using Submarine.Core.Entities;
using Submarine.Core.Profiles;
using Submarine.Core.Quality;
using Shouldly;
using Xunit;

namespace Submarine.Core.Tests.Profiles;

public class QualityDefinitionSizeCheckerTest
{
	[Fact]
	public void IsWithinSize_ShouldReturnTrue_WhenDefinitionIsMissing()
	{
		((QualityDefinition?)null).IsWithinSize(1024, 60).ShouldBeTrue();
	}

	[Fact]
	public void IsWithinSize_ShouldReturnTrue_WhenSizeOrRuntimeIsUnknown()
	{
		var definition = Definition(min: 1, max: 10);

		definition.IsWithinSize(null, 60).ShouldBeTrue();
		definition.IsWithinSize(1024L * 1024 * 1024, null).ShouldBeTrue();
		definition.IsWithinSize(null, null).ShouldBeTrue();
	}

	[Fact]
	public void IsWithinSize_ShouldReturnTrue_WhenWithinLimits()
	{
		// 60 minutes at 2 to 10 MB per minute allows 120 to 600 MB
		var definition = Definition(min: 2, max: 10);

		definition.IsWithinSize(200L * 1024 * 1024, 60).ShouldBeTrue();
		definition.IsWithinSize(600L * 1024 * 1024, 60).ShouldBeTrue();
	}

	[Fact]
	public void IsWithinSize_ShouldReturnFalse_WhenBelowMinimum()
	{
		var definition = Definition(min: 2, max: 10);

		definition.IsWithinSize(100L * 1024 * 1024, 60).ShouldBeFalse();
	}

	[Fact]
	public void IsWithinSize_ShouldReturnFalse_WhenAboveMaximum()
	{
		var definition = Definition(min: 2, max: 10);

		definition.IsWithinSize(700L * 1024 * 1024, 60).ShouldBeFalse();
	}

	[Fact]
	public void IsWithinSize_ShouldReturnTrue_WhenLimitsAreNull()
	{
		Definition(min: null, max: null).IsWithinSize(long.MaxValue, 60).ShouldBeTrue();
		Definition(min: 2, max: null).IsWithinSize(long.MaxValue, 60).ShouldBeTrue();
		Definition(min: null, max: 10).IsWithinSize(1, 60).ShouldBeTrue();
	}

	[Fact]
	public void IsWithinSize_ShouldScaleByEpisodeCount()
	{
		// a two episode file has twice the minutes, so 1000 MB over 2x60 minutes stays inside 5 MB per minute
		var definition = Definition(min: 5, max: 10);

		definition.IsWithinSize(1000L * 1024 * 1024, 60, episodeCount: 2).ShouldBeTrue();
		definition.IsWithinSize(1000L * 1024 * 1024, 60, episodeCount: 1).ShouldBeFalse();
	}

	private static QualityDefinition Definition(double? min, double? max)
		=> new()
		{
			Source = QualitySource.WEB_DL,
			Resolution = QualityResolution.R1080_P,
			Title = "WebDL-1080p",
			MinSizeMbPerMinute = min,
			MaxSizeMbPerMinute = max
		};
}
