using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.MediaFiles;
using Submarine.Core.Release;
using Xunit;

namespace Submarine.Infrastructure.Tests.MediaFiles;

public sealed class EpisodeMatcherTests
{
	private static Series StandardSeries(SeriesNumbering numbering = SeriesNumbering.AIRED)
		=> new() { Id = 1, Title = "Test Show", Type = SeriesType.STANDARD, Numbering = numbering };

	private static List<Episode> Episodes(int seriesId, params (int Season, int Episode)[] pairs)
		=> [.. pairs.Select((p, index) => new Episode { Id = index + 1, SeriesId = seriesId, SeasonNumber = p.Season, EpisodeNumber = p.Episode })];

	[Fact]
	public void Match_ShouldFindSingleEpisode_WhenSeasonAndEpisodeMatch()
	{
		var series = StandardSeries();
		var episodes = Episodes(series.Id, (1, 1), (1, 2), (1, 3));
		var data = new SeriesReleaseData { Seasons = [1], Episodes = [2] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.EpisodeNumber).ShouldBe([2]);
	}

	[Fact]
	public void Match_ShouldFindMultipleEpisodes_ForMultiEpisodeRelease()
	{
		var series = StandardSeries();
		var episodes = Episodes(series.Id, (1, 1), (1, 2), (1, 3));
		var data = new SeriesReleaseData { Seasons = [1], Episodes = [1, 2] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.EpisodeNumber).OrderBy(x => x).ShouldBe([1, 2]);
	}

	[Fact]
	public void Match_ShouldFindFullSeasonPack_WhenOnlySeasonKnown()
	{
		var series = StandardSeries();
		var episodes = Episodes(series.Id, (1, 1), (1, 2), (2, 1));
		var data = new SeriesReleaseData { Seasons = [1], Episodes = [] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.EpisodeNumber).OrderBy(x => x).ShouldBe([1, 2]);
	}

	[Fact]
	public void Match_ShouldUseAbsoluteNumber_WhenSeriesUsesAbsoluteNumbering()
	{
		var series = StandardSeries(SeriesNumbering.ABSOLUTE);
		var episodes = Episodes(series.Id, (1, 1), (1, 2));
		episodes[0].AbsoluteEpisodeNumber = 1;
		episodes[1].AbsoluteEpisodeNumber = 2;
		var data = new SeriesReleaseData { AbsoluteEpisodes = [2] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.EpisodeNumber).ShouldBe([2]);
	}

	[Fact]
	public void Match_ShouldUseAbsoluteNumber_WhenNoSeasonParsed()
	{
		var series = StandardSeries();
		var episodes = Episodes(series.Id, (1, 1), (1, 2));
		episodes[1].AbsoluteEpisodeNumber = 14;
		var data = new SeriesReleaseData { AbsoluteEpisodes = [14] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.EpisodeNumber).ShouldBe([2]);
	}

	[Fact]
	public void Match_ShouldUseSceneNumbering_WhenSeriesUsesDvdNumbering()
	{
		var series = StandardSeries(SeriesNumbering.DVD);
		var episodes = Episodes(series.Id, (1, 1));
		episodes[0].SceneSeasonNumber = 2;
		episodes[0].SceneEpisodeNumber = 5;
		var data = new SeriesReleaseData { Seasons = [2], Episodes = [5] };

		var matched = EpisodeMatcher.Match(episodes, series, data);

		matched.Select(x => x.Id).ShouldBe([episodes[0].Id]);
	}

	[Fact]
	public void Match_ShouldReturnEmpty_WhenNoDataParsed()
		=> EpisodeMatcher.Match(Episodes(1, (1, 1)), StandardSeries(), null).ShouldBeEmpty();

	[Fact]
	public void MatchDaily_ShouldFindEpisode_ByAirDateInFileName()
	{
		var episodes = new List<Episode>
		{
			new() { Id = 1, SeriesId = 1, SeasonNumber = 2024, EpisodeNumber = 1, AirDate = "2024-01-15" },
			new() { Id = 2, SeriesId = 1, SeasonNumber = 2024, EpisodeNumber = 2, AirDate = "2024-01-16" }
		};

		var matched = EpisodeMatcher.MatchDaily(episodes, "Late.Night.Show.2024.01.16.HDTV.x264");

		matched.Select(x => x.Id).ShouldBe([2]);
	}

	[Fact]
	public void MatchDaily_ShouldReturnEmpty_WhenNoDateInFileName()
	{
		var episodes = new List<Episode> { new() { Id = 1, SeriesId = 1, AirDate = "2024-01-15" } };

		EpisodeMatcher.MatchDaily(episodes, "Late.Night.Show.HDTV.x264").ShouldBeEmpty();
	}
}
