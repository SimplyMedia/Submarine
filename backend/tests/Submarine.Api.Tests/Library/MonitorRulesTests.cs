using Shouldly;
using Xunit;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Library;

namespace Submarine.Api.Tests.Library;

/// <summary>
///     Monitor option application over seasons and episodes.
/// </summary>
public sealed class MonitorRulesTests
{
	private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Apply_ShouldMonitorEverything_WhenOptionIsAll()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.ALL, monitorSpecials: false, Now);
		series.Seasons.Where(x => x.SeasonNumber > 0).All(x => x.Monitored).ShouldBeTrue();
		series.Episodes.Where(x => x.SeasonNumber > 0).All(x => x.Monitored).ShouldBeTrue();
		series.Seasons.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeFalse();
		series.Episodes.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeFalse();
	}

	[Fact]
	public void Apply_ShouldMonitorNothing_WhenOptionIsNone()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.NONE, monitorSpecials: false, Now);
		series.Seasons.All(x => !x.Monitored).ShouldBeTrue();
		series.Episodes.All(x => !x.Monitored).ShouldBeTrue();
	}

	[Fact]
	public void Apply_ShouldMonitorOnlyFutureEpisodes_WhenOptionIsFuture()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.FUTURE, monitorSpecials: false, Now);
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 1).Monitored.ShouldBeFalse();
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 2).Monitored.ShouldBeTrue();
		series.Episodes.Single(x => x.SeasonNumber == 2 && x.EpisodeNumber == 1).Monitored.ShouldBeTrue();
	}

	[Fact]
	public void Apply_ShouldMonitorOnlyAiredEpisodes_WhenOptionIsExisting()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.EXISTING, monitorSpecials: false, Now);
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 1).Monitored.ShouldBeTrue();
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 2).Monitored.ShouldBeFalse();
	}

	[Fact]
	public void Apply_ShouldMonitorMissingEpisodes_WhenOptionIsMissing()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.MISSING, monitorSpecials: false, Now);
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 1).Monitored.ShouldBeFalse();
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 2).Monitored.ShouldBeTrue();
		series.Episodes.Single(x => x.SeasonNumber == 2 && x.EpisodeNumber == 1).Monitored.ShouldBeTrue();
	}

	[Fact]
	public void Apply_ShouldMonitorOnlyThePilot_WhenOptionIsPilot()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.PILOT, monitorSpecials: false, Now);
		series.Episodes.Single(x => x.SeasonNumber == 1 && x.EpisodeNumber == 1).Monitored.ShouldBeTrue();
		series.Episodes.Where(x => x.SeasonNumber == 1 && x.EpisodeNumber > 1)
			.All(x => !x.Monitored)
			.ShouldBeTrue();
		series.Seasons.Single(x => x.SeasonNumber == 1).Monitored.ShouldBeTrue();
		series.Seasons.Single(x => x.SeasonNumber == 2).Monitored.ShouldBeFalse();
	}

	[Fact]
	public void Apply_ShouldMonitorFirstSeason_WhenOptionIsFirstSeason()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.FIRST_SEASON, monitorSpecials: false, Now);
		series.Seasons.Single(x => x.SeasonNumber == 1).Monitored.ShouldBeTrue();
		series.Seasons.Single(x => x.SeasonNumber == 2).Monitored.ShouldBeFalse();
		series.Episodes.Where(x => x.SeasonNumber == 1).All(x => x.Monitored).ShouldBeTrue();
		series.Episodes.Where(x => x.SeasonNumber == 2).All(x => !x.Monitored).ShouldBeTrue();
	}

	[Fact]
	public void Apply_ShouldMonitorLatestSeason_WhenOptionIsLatestSeason()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.LATEST_SEASON, monitorSpecials: false, Now);
		series.Seasons.Single(x => x.SeasonNumber == 1).Monitored.ShouldBeFalse();
		series.Seasons.Single(x => x.SeasonNumber == 2).Monitored.ShouldBeTrue();
		series.Episodes.Where(x => x.SeasonNumber == 2).All(x => x.Monitored).ShouldBeTrue();
	}

	[Fact]
	public void Apply_ShouldExcludeSpecials_WhenMonitorSpecialsIsOff()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.ALL, monitorSpecials: false, Now);
		series.Seasons.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeFalse();
		series.Episodes.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeFalse();
	}

	[Fact]
	public void Apply_ShouldIncludeSpecials_WhenMonitorSpecialsIsOn()
	{
		var series = TestSeries();
		MonitorRules.Apply(series, AddMonitorOption.ALL, monitorSpecials: true, Now);
		series.Seasons.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeTrue();
		series.Episodes.Single(x => x.SeasonNumber == 0).Monitored.ShouldBeTrue();
	}

	private static Series TestSeries()
	{
		var series = new Series { Title = "Test", TvdbId = 1 };
		series.Seasons.Add(new Season { SeriesId = 1, SeasonNumber = 0 });
		series.Seasons.Add(new Season { SeriesId = 1, SeasonNumber = 1 });
		series.Seasons.Add(new Season { SeriesId = 1, SeasonNumber = 2 });
		series.Episodes.Add(new Episode
		{
			SeriesId = 1,
			SeasonNumber = 0,
			EpisodeNumber = 1,
			AirDateUtc = Now.AddDays(-30)
		});
		series.Episodes.Add(new Episode
		{
			SeriesId = 1,
			SeasonNumber = 1,
			EpisodeNumber = 1,
			AirDateUtc = Now.AddDays(-30)
		});
		series.Episodes.Add(new Episode
		{
			SeriesId = 1,
			SeasonNumber = 1,
			EpisodeNumber = 2,
			AirDateUtc = Now.AddDays(7)
		});
		series.Episodes.Add(new Episode
		{
			SeriesId = 1,
			SeasonNumber = 2,
			EpisodeNumber = 1,
			AirDateUtc = null
		});
		return series;
	}
}
