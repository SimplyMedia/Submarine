using System.Net;
using Shouldly;
using Submarine.Contracts.Metadata;
using Submarine.Metadata.Tests.Support;
using Xunit;

namespace Submarine.Metadata.Tests;

public class TmdbSeriesTests
{
	[Fact]
	public async Task GetSeriesByTmdb_ShouldMergeAiredAbsoluteAndDvdOrderings_WhenEpisodeGroupsExist()
	{
		var handler = new StubHttpMessageHandler().EnqueueTmdbSeriesWithGroups();
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTmdbAsync(1399, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.TvdbId.ShouldBe(121361);
		series.TmdbId.ShouldBe(1399);
		series.ImdbId.ShouldBe("tt0944947");
		series.Title.ShouldBe("Game of Thrones");
		series.SortTitle.ShouldBe("game of thrones");
		series.Status.ShouldBe(SeriesStatus.ENDED);
		series.Network.ShouldBe("HBO");
		series.Runtime.ShouldBe(60);
		series.Certification.ShouldBe("TV-MA");
		series.Year.ShouldBe(2011);
		series.PosterUrl.ShouldBe("https://image.tmdb.org/t/p/original/1XS1oqL89opfnbLl8WnZY1O1lJx.jpg");
		series.Seasons.Select(s => s.Name).ShouldBe(["Season 1", "Season 2"]);
		series.Episodes.Count.ShouldBe(3);
		series.AlternateTitles.ShouldContain(new AlternateTitleResource("Game of Thrones: Das Lied von Eis und Feuer", "DE"));

		series.Episodes[0].TmdbId.ShouldBe(63056);
		series.Episodes[0].ImageUrl.ShouldBe("https://image.tmdb.org/t/p/original/wGFUewXPeMErCe2fCuw3JI4JsCI.jpg");
		series.Episodes[0].Numbers.ShouldBe(
		[
			new EpisodeNumber(EpisodeOrdering.AIRED, 1, 1, null),
			new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 1),
			new EpisodeNumber(EpisodeOrdering.DVD, 1, 1, null)
		]);
		series.Episodes[1].Numbers.ShouldBe(
		[
			new EpisodeNumber(EpisodeOrdering.AIRED, 1, 2, null),
			new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 2),
			new EpisodeNumber(EpisodeOrdering.DVD, 1, 2, null)
		]);
		series.Episodes[2].Numbers.ShouldBe(
		[
			new EpisodeNumber(EpisodeOrdering.AIRED, 2, 1, null),
			new EpisodeNumber(EpisodeOrdering.ABSOLUTE, null, null, 3),
			new EpisodeNumber(EpisodeOrdering.DVD, 2, 1, null)
		]);

		handler.RequestPaths[0].ShouldContain("append_to_response=external_ids,alternative_titles,content_ratings,episode_groups");
		handler.RequestPaths.ShouldContain("/3/tv/1399/season/1");
		handler.RequestPaths.ShouldContain("/3/tv/1399/season/2");
		handler.RequestPaths.ShouldContain("/3/tv/episode_group/group-absolute");
		handler.RequestPaths.ShouldContain("/3/tv/episode_group/group-dvd");
	}

	[Fact]
	public async Task GetSeriesByTmdb_ShouldOnlyEmitAiredOrdering_WhenNoEpisodeGroupsExist()
	{
		var handler = new StubHttpMessageHandler().EnqueueTmdbSeriesWithoutGroups();
		using var host = MetadataTestHost.Create(handler);

		var series = await host.Service.GetSeriesByTmdbAsync(1399, TestContext.Current.CancellationToken);

		series.ShouldNotBeNull();
		series.Status.ShouldBe(SeriesStatus.CONTINUING);
		series.Episodes.Count.ShouldBe(2);
		series.Episodes[0].Numbers.ShouldBe([new EpisodeNumber(EpisodeOrdering.AIRED, 1, 1, null)]);
		series.Episodes[1].Numbers.ShouldBe([new EpisodeNumber(EpisodeOrdering.AIRED, 1, 2, null)]);
	}
}
