using Submarine.Contracts.Mappings;
using Shouldly;
using Xunit;

using Submarine.Mappings.Entities;
using Submarine.Mappings.Services;
using Submarine.Mappings.Tests.TestKit;

namespace Submarine.Mappings.Tests;

public sealed class MappingResolverTests : SqliteTestBase
{
	[Fact]
	public async Task ResolveScene_ShouldReturnEpisodeOverride_WhenPerEpisodeMappingExists()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 2, EpisodeOffset = 10 });
		Db.SceneEpisodeMappings.Add(new SceneEpisodeMapping { TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 1, SceneSeasonNumber = 2, SceneEpisodeNumber = 20 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(1, 1, 1)).ShouldBe(new SceneResolution(2, 20));
	}

	[Fact]
	public async Task ResolveScene_ShouldPreferExactSeasonMapping_OverWildcard()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Wildcard", SeasonNumber = null, SceneSeasonNumber = 5, EpisodeOffset = 0 });
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Exact", SeasonNumber = 2, SceneSeasonNumber = 3, EpisodeOffset = 0 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(1, 2, 7)).ShouldBe(new SceneResolution(3, 7));
		(await resolver.ResolveSceneAsync(1, 4, 7)).ShouldBe(new SceneResolution(5, 7));
	}

	[Fact]
	public async Task ResolveScene_ShouldKeepTvdbSeason_WhenSceneSeasonNumberIsNull()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = null, EpisodeOffset = 0 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(1, 1, 3)).ShouldBe(new SceneResolution(1, 3));
	}

	[Fact]
	public async Task ResolveScene_ShouldApplyEpisodeOffset()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 100 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(1, 1, 4)).ShouldBe(new SceneResolution(1, 104));
	}

	[Fact]
	public async Task ResolveScene_ShouldPassthrough_WhenSeriesIsUnmapped()
	{
		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(42, 7, 8)).ShouldBe(new SceneResolution(7, 8));
	}

	[Fact]
	public async Task ResolveScene_ShouldIgnoreMappingsOfOtherSeries()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 2, Title = "Other show", SeasonNumber = 1, SceneSeasonNumber = 9, EpisodeOffset = 0 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveSceneAsync(1, 1, 1)).ShouldBe(new SceneResolution(1, 1));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReturnEpisodeOverride_WhenSceneEpisodeMatches()
	{
		Db.SceneEpisodeMappings.Add(new SceneEpisodeMapping { TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 1, SceneSeasonNumber = 2, SceneEpisodeNumber = 20 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveTvdbAsync(1, 2, 20)).ShouldBe(new TvdbResolution(1, 1, 1, 0));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldPreferExactSceneSeason_OverWildcard()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Wildcard", SeasonNumber = 8, SceneSeasonNumber = null, EpisodeOffset = 0 });
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Exact", SeasonNumber = 3, SceneSeasonNumber = 5, EpisodeOffset = 0 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveTvdbAsync(1, 5, 2)).ShouldBe(new TvdbResolution(1, 3, 2, 0));
		(await resolver.ResolveTvdbAsync(1, 9, 2)).ShouldBe(new TvdbResolution(1, 8, 2, 0));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldSubtractEpisodeOffset()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 100 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveTvdbAsync(1, 1, 104)).ShouldBe(new TvdbResolution(1, 1, 4, 0));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldKeepSceneSeason_WhenSeasonNumberIsNull()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = null, SceneSeasonNumber = 6, EpisodeOffset = 0 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		(await resolver.ResolveTvdbAsync(1, 6, 3)).ShouldBe(new TvdbResolution(1, 6, 3, 0));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldPassthrough_WhenSeriesIsUnmapped()
	{
		var resolver = new MappingResolver(Db);

		(await resolver.ResolveTvdbAsync(42, 7, 8)).ShouldBe(new TvdbResolution(42, 7, 8, 0));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldInvertSceneResolution_WhenMappingIsInvertible()
	{
		Db.SceneMappings.Add(new SceneMapping { TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 5 });
		Db.SceneEpisodeMappings.Add(new SceneEpisodeMapping { TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 1, SceneSeasonNumber = 2, SceneEpisodeNumber = 2 });
		await Db.SaveChangesAsync();

		var resolver = new MappingResolver(Db);

		var forward = await resolver.ResolveSceneAsync(1, 1, 1);
		(await resolver.ResolveTvdbAsync(1, forward.SceneSeason, forward.SceneEpisode)).ShouldBe(new TvdbResolution(1, 1, 1, 0));

		var seasonForward = await resolver.ResolveSceneAsync(1, 1, 12);
		(await resolver.ResolveTvdbAsync(1, seasonForward.SceneSeason, seasonForward.SceneEpisode)).ShouldBe(new TvdbResolution(1, 1, 12, 0));
	}
}
