using Submarine.Contracts.Mappings;
using Shouldly;
using Xunit;

using Submarine.Mappings.Entities;
using Submarine.Mappings.Services;
using Submarine.Mappings.Tests.TestKit;

namespace Submarine.Mappings.Tests;

public sealed class AniListMappingServiceTests : SqliteTestBase
{
	[Fact]
	public async Task ResolveTvdb_ShouldReturnFirstEpisode_WhenEpisodeIsOne()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 1)).ShouldBe(new TvdbResolution(1, 1, 1, 1));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReturnLastEpisode_WhenEpisodeEqualsCount()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 12)).ShouldBe(new TvdbResolution(1, 1, 12, 12));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReturnNull_WhenEpisodeExceedsCount()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 13)).ShouldBeNull();
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReturnNull_WhenEpisodeIsZeroOrNegative()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 0)).ShouldBeNull();
		(await service.ResolveTvdbAsync(100, -3)).ShouldBeNull();
	}

	[Fact]
	public async Task ResolveTvdb_ShouldResolveBeyondCount_WhenCountIsOpenEnded()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Ongoing", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = null, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 50)).ShouldBe(new TvdbResolution(1, 1, 50, 50));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldApplyAbsoluteOffset()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 2", TvdbSeason = 1, EpisodeStart = 13, EpisodeCount = 12, AbsoluteOffset = 12 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(100, 1)).ShouldBe(new TvdbResolution(1, 1, 13, 13));
	}

	[Fact]
	public async Task ResolveTvdb_ShouldReturnNull_WhenAniListIdIsUnmapped()
	{
		var service = new AniListMappingService(Db);

		(await service.ResolveTvdbAsync(999, 1)).ShouldBeNull();
	}

	[Fact]
	public async Task ResolveAniList_ShouldReturnRelativeEpisode_WhenEpisodeIsInsideCour()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		Db.AniListMappings.Add(new AniListMapping { AniListId = 101, TvdbId = 1, Title = "Cour 2", TvdbSeason = 1, EpisodeStart = 13, EpisodeCount = 12, AbsoluteOffset = 12 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveAniListAsync(1, 1, 13)).ShouldBe(new AniListResolution(101, 1));
		(await service.ResolveAniListAsync(1, 1, 24)).ShouldBe(new AniListResolution(101, 12));
		(await service.ResolveAniListAsync(1, 1, 1)).ShouldBe(new AniListResolution(100, 1));
	}

	[Fact]
	public async Task ResolveAniList_ShouldReturnNull_WhenEpisodeFallsIntoGapBetweenCours()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Cour 1", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		Db.AniListMappings.Add(new AniListMapping { AniListId = 101, TvdbId = 1, Title = "Cour 2", TvdbSeason = 1, EpisodeStart = 14, EpisodeCount = 12, AbsoluteOffset = 13 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveAniListAsync(1, 1, 13)).ShouldBeNull();
	}

	[Fact]
	public async Task ResolveAniList_ShouldReturnNull_WhenSeasonIsUnmapped()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "Season 2", TvdbSeason = 2, EpisodeStart = 1, EpisodeCount = null, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		(await service.ResolveAniListAsync(1, 1, 1)).ShouldBeNull();
	}

	[Fact]
	public async Task GetForSeries_ShouldOrderBySeasonThenStartEpisode()
	{
		Db.AniListMappings.Add(new AniListMapping { AniListId = 102, TvdbId = 1, Title = "S2", TvdbSeason = 2, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		Db.AniListMappings.Add(new AniListMapping { AniListId = 101, TvdbId = 1, Title = "S1 later", TvdbSeason = 1, EpisodeStart = 13, EpisodeCount = 12, AbsoluteOffset = 0 });
		Db.AniListMappings.Add(new AniListMapping { AniListId = 100, TvdbId = 1, Title = "S1 first", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = 12, AbsoluteOffset = 0 });
		await Db.SaveChangesAsync();

		var service = new AniListMappingService(Db);

		var result = await service.GetForSeriesAsync(1);
		result.Select(m => m.AniListId).ShouldBe([100, 101, 102]);
	}
}
