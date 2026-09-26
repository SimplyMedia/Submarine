using NSubstitute;
using Shouldly;
using Submarine.Contracts.Mappings;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Core.Indexers;
using Submarine.Infrastructure.Mappings;
using Submarine.Infrastructure.Search;
using Xunit;

namespace Submarine.Infrastructure.Tests.Search;

public sealed class EpisodeSearchPlannerTests
{
	private readonly IMappingsClient _mappings = Substitute.For<IMappingsClient>();
	private readonly EpisodeSearchPlanner _planner;

	public EpisodeSearchPlannerTests() => _planner = new EpisodeSearchPlanner(_mappings);

	[Fact]
	public async Task BuildAsync_ShouldUseDailyAirDateWithoutMappingLookup()
	{
		var series = new Series { TvdbId = 1, Title = "Daily Show", Type = SeriesType.DAILY };
		var episode = new Episode { SeasonNumber = 2, EpisodeNumber = 3, AirDateUtc = new DateTime(2025, 4, 5, 23, 30, 0, DateTimeKind.Utc) };

		var requests = await _planner.BuildAsync(series, episode, TestContext.Current.CancellationToken);

		requests.Count.ShouldBe(1);
		requests[0].ShouldBeOfType<TvSearchRequest>().Query.ShouldBe("Daily Show 2025.04.05");
		await _mappings.DidNotReceive().ResolveSceneAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BuildAsync_ShouldBuildAnimeAbsoluteAndStandardQueriesUsingMappedSceneNumbers()
	{
		_mappings.ResolveSceneAsync(12, 1, 4, Arg.Any<CancellationToken>()).Returns(new SceneResolution(2, 7));
		var series = new Series { TvdbId = 12, Title = "Anime", Type = SeriesType.ANIME };
		var episode = new Episode { SeasonNumber = 1, EpisodeNumber = 4, AbsoluteEpisodeNumber = 17 };

		var requests = await _planner.BuildAsync(series, episode, TestContext.Current.CancellationToken);

		requests.Count.ShouldBe(2);
		requests[0].ShouldBeOfType<TvSearchRequest>().Query.ShouldBe("Anime 017");
		var standard = requests[1].ShouldBeOfType<TvSearchRequest>();
		standard.Query.ShouldBe("Anime S02E07");
		(standard.Season, standard.Episode).ShouldBe((2, 7));
	}

	[Fact]
	public async Task BuildAsync_ShouldPreferStoredSceneNumbersAndUseAiredNumbersWhenMappingUnavailable()
	{
		var series = new Series { TvdbId = 4, Title = "Show" };
		var stored = new Episode { SeasonNumber = 1, EpisodeNumber = 2, SceneSeasonNumber = 3, SceneEpisodeNumber = 8 };
		var storedRequest = (TvSearchRequest)(await _planner.BuildAsync(series, stored, TestContext.Current.CancellationToken))[0];
		storedRequest.Query.ShouldBe("Show S03E08");
		await _mappings.DidNotReceive().ResolveSceneAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

		_mappings.ResolveSceneAsync(4, 1, 2, Arg.Any<CancellationToken>()).Returns<Task<SceneResolution>>(_ => throw new HttpRequestException("offline"));
		var fallback = (TvSearchRequest)(await _planner.BuildAsync(series, new Episode { SeasonNumber = 1, EpisodeNumber = 2 }, TestContext.Current.CancellationToken))[0];
		fallback.Query.ShouldBe("Show S01E02");
	}
}
