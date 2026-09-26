using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Release;
using Submarine.Core.Search;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class EpisodeSelectorTests
{
	private static Episode Episode(int id, int season, int episode, int? absolute = null, int? sceneSeason = null, int? sceneEpisode = null, int? sceneAbsolute = null, DateTime? airDate = null)
		=> new()
		{
			Id = id,
			SeasonNumber = season,
			EpisodeNumber = episode,
			AbsoluteEpisodeNumber = absolute,
			SceneSeasonNumber = sceneSeason,
			SceneEpisodeNumber = sceneEpisode,
			SceneAbsoluteEpisodeNumber = sceneAbsolute,
			AirDateUtc = airDate
		};

	[Fact]
	public void Select_ShouldReturnAllEpisodesOfSeason_WhenFullSeason()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1), Episode(2, 1, 2), Episode(3, 2, 1) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.FULL_SEASON, Seasons = [1] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1, 2], ignoreOrder: true);
	}

	[Fact]
	public void Select_ShouldReturnEpisodesOfEverySeason_WhenMultiSeason()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1), Episode(2, 2, 1), Episode(3, 3, 1) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.MULTI_SEASON, Seasons = [1, 2] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1, 2], ignoreOrder: true);
	}

	[Fact]
	public void Select_ShouldReturnOnlyListedEpisodes_WhenMultiEpisode()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1), Episode(2, 1, 2), Episode(3, 1, 3) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.MULTI_EPISODES, Seasons = [1], Episodes = [1, 3] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1, 3], ignoreOrder: true);
	}

	[Fact]
	public void Select_ShouldMatchByAbsoluteNumber_WhenReleaseHasAbsoluteEpisodes()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1, absolute: 12), Episode(2, 1, 2, absolute: 13) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.EPISODE, AbsoluteEpisodes = [12] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1]);
	}

	[Fact]
	public void Select_ShouldFallBackToSceneAbsoluteNumber_WhenAiredAbsoluteMissing()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1, absolute: null, sceneAbsolute: 12) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.EPISODE, AbsoluteEpisodes = [12] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1]);
	}

	[Fact]
	public void Select_ShouldFallBackToSceneSeasonNumber_WhenAiredSeasonDoesNotMatch()
	{
		// the release reports season 2 in scene numbering, but this episode airs as season 1 episode 3
		var episodes = new List<Episode> { Episode(1, 1, 3, sceneSeason: 2, sceneEpisode: 3) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.EPISODE, Seasons = [2], Episodes = [3] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([1]);
	}

	[Fact]
	public void Select_ShouldMatchByAirDate_WhenDailyAirDateGiven()
	{
		var episodes = new List<Episode>
		{
			Episode(1, 1, 1, airDate: new DateTime(2024, 3, 7)),
			Episode(2, 1, 2, airDate: new DateTime(2024, 3, 8))
		};

		var result = EpisodeSelector.Select(null, episodes, dailyAirDate: new DateTime(2024, 3, 7));

		result.ShouldBe([1]);
	}

	[Fact]
	public void Select_ShouldReturnEmpty_WhenNoDataAndNoDailyDate()
	{
		var episodes = new List<Episode> { Episode(1, 1, 1) };

		var result = EpisodeSelector.Select(null, episodes);

		result.ShouldBeEmpty();
	}

	[Fact]
	public void Select_ShouldMatchSpecials_ByEpisodeNumberWithinSeasonZero()
	{
		var episodes = new List<Episode> { Episode(1, 0, 1), Episode(2, 0, 2), Episode(3, 1, 1) };
		var data = new SeriesReleaseData { ReleaseType = SeriesReleaseType.SPECIAL, Episodes = [2] };

		var result = EpisodeSelector.Select(data, episodes);

		result.ShouldBe([2]);
	}
}
