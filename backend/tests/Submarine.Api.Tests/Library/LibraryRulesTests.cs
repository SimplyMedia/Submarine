using Shouldly;
using Xunit;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;

namespace Submarine.Api.Tests.Library;

/// <summary>
///     Title cleaning and sorting.
/// </summary>
public sealed class TitleNormalizerTests
{
	[Theory]
	[InlineData("The Matrix", "thematrix")]
	[InlineData("Rick & Morty!", "rickmorty")]
	[InlineData("Anime: Rising", "animerising")]
	public void CleanTitle_ShouldKeepLowercaseAlphanumericOnly(string title, string expected)
		=> TitleNormalizer.CleanTitle(title).ShouldBe(expected);

	[Theory]
	[InlineData("The Boys", "boys")]
	[InlineData("A Series", "series")]
	[InlineData("An Offer", "offer")]
	[InlineData("Star Trek", "star trek")]
	public void SortTitle_ShouldDropLeadingArticles(string title, string expected)
		=> TitleNormalizer.SortTitle(title).ShouldBe(expected);
}

/// <summary>
///     Movie availability rules.
/// </summary>
public sealed class MovieAvailabilityTests
{
	private static readonly DateTime Now = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void IsAvailable_ShouldBeFalse_WithoutAnyDate()
		=> MovieAvailability.IsAvailable(Movie(null, null, null), MinimumAvailability.ANNOUNCED, 0, Now)
			.ShouldBeFalse();

	[Fact]
	public void IsAvailable_ShouldUseAnyDate_WhenAnnounced()
		=> MovieAvailability.IsAvailable(Movie(null, null, Now.AddDays(-1)), MinimumAvailability.ANNOUNCED, 0, Now)
			.ShouldBeTrue();

	[Fact]
	public void IsAvailable_ShouldUseInCinemasDate_WhenInCinemas()
	{
		var movie = Movie(Now.AddDays(-1), Now.AddDays(5), null);
		MovieAvailability.IsAvailable(movie, MinimumAvailability.IN_CINEMAS, 0, Now).ShouldBeTrue();
		MovieAvailability.IsAvailable(movie, MinimumAvailability.IN_CINEMAS, 2, Now).ShouldBeFalse();
	}

	[Fact]
	public void IsAvailable_ShouldPreferPhysical_WhenReleased()
	{
		var movie = Movie(Now.AddDays(-10), Now.AddDays(-1), Now.AddDays(-5));
		MovieAvailability.IsAvailable(movie, MinimumAvailability.RELEASED, 0, Now).ShouldBeTrue();
	}

	[Fact]
	public void IsAvailable_ShouldFallBackToInCinemas_WhenReleasedWithoutDates()
		=> MovieAvailability.IsAvailable(Movie(Now.AddDays(-1), null, null), MinimumAvailability.RELEASED, 0, Now)
			.ShouldBeTrue();

	[Fact]
	public void IsAvailable_ShouldRespectDelay()
		=> MovieAvailability.IsAvailable(Movie(Now.AddDays(-1), null, null), MinimumAvailability.ANNOUNCED, 3, Now)
			.ShouldBeFalse();

	private static Submarine.Core.Entities.Movie Movie(DateTime? inCinemas, DateTime? digital, DateTime? physical)
		=> new()
		{
			TmdbId = 1,
			Title = "Test",
			InCinemasDate = inCinemas,
			DigitalReleaseDate = digital,
			PhysicalReleaseDate = physical
		};
}

/// <summary>
///     Next and previous airing computation.
/// </summary>
public sealed class AiringRulesTests
{
	private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void NextAiring_ShouldReturnEarliestFutureMonitoredEpisode()
	{
		var episode = AiringRules.NextAiring(
		[
			Ep(1, Now.AddDays(-1)),
			Ep(2, Now.AddDays(2)),
			Ep(3, Now.AddDays(1))
		], Now);
		episode!.EpisodeNumber.ShouldBe(3);
	}

	[Fact]
	public void NextAiring_ShouldIgnoreUnmonitoredAndPastEpisodes()
	{
		var unmonitored = Ep(1, Now.AddDays(1));
		unmonitored.Monitored = false;
		AiringRules.NextAiring([unmonitored, Ep(2, Now.AddDays(-1))], Now).ShouldBeNull();
	}

	[Fact]
	public void PreviousAiring_ShouldReturnLatestPastMonitoredEpisode()
	{
		var episode = AiringRules.PreviousAiring(
		[
			Ep(1, Now.AddDays(-5)),
			Ep(2, Now.AddDays(-1)),
			Ep(3, Now.AddDays(1))
		], Now);
		episode!.EpisodeNumber.ShouldBe(2);
	}

	private static Episode Ep(int number, DateTime airDateUtc)
		=> new() { SeasonNumber = 1, EpisodeNumber = number, AirDateUtc = airDateUtc };
}

/// <summary>
///     Season statistics computation.
/// </summary>
public sealed class SeasonStatisticsTests
{
	[Fact]
	public void Compute_ShouldCountMonitoredEpisodesFilesAndSize()
	{
		var file1 = new EpisodeFile { Size = 1000 };
		var file2 = new EpisodeFile { Size = 500 };
		var monitoredWithFile = new Episode { Monitored = true };
		monitoredWithFile.Files.Add(file1);
		monitoredWithFile.Files.Add(file2);
		var monitoredWithoutFile = new Episode { Monitored = true };
		var unmonitored = new Episode { Monitored = false };

		var stats = Submarine.Core.Library.SeasonStatisticsCalculator.Compute(
			[monitoredWithFile, monitoredWithoutFile, unmonitored]);

		stats.EpisodeCount.ShouldBe(2);
		stats.EpisodeFileCount.ShouldBe(1);
		stats.TotalEpisodeCount.ShouldBe(3);
		stats.SizeOnDisk.ShouldBe(1500);
		stats.PercentOfEpisodes.ShouldBe(50.0);
	}

	[Fact]
	public void Compute_ShouldReportFullPercent_WithoutMonitoredEpisodes()
	{
		var stats = Submarine.Core.Library.SeasonStatisticsCalculator.Compute([]);
		stats.EpisodeCount.ShouldBe(0);
		stats.PercentOfEpisodes.ShouldBe(100.0);
	}
}
