using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;
using Submarine.Mappings.Services;
using Xunit;

namespace Submarine.Mappings.Tests.Services;

public class AniListMappingServiceTest : IDisposable
{
	private readonly string _dbPath;
	private readonly MappingsDatabaseContext _context;
	private readonly AniListMappingService _service;

	public AniListMappingServiceTest()
	{
		_dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:SqliteConnection"] = $"Data Source={_dbPath}"
			})
			.Build();

		_context = new MappingsDatabaseContext(new DbContextOptionsBuilder<MappingsDatabaseContext>().Options,
			configuration);
		_context.Database.EnsureCreated();

		_service = new AniListMappingService(_context);
	}

	private async Task SeedMultiCourAsync()
	{
		// TVDB season 4 split into two AniList entries, TVDB absolute numbering continues across the split
		_context.AniListMappings.Add(new AniListMapping
		{
			AniListId = 100, TvdbId = 1, Title = "Part 1", TvdbSeason = 4, EpisodeStart = 1, EpisodeCount = 16,
			AbsoluteOffset = 59
		});
		_context.AniListMappings.Add(new AniListMapping
		{
			AniListId = 101, TvdbId = 1, Title = "Part 2", TvdbSeason = 4, EpisodeStart = 17, EpisodeCount = 12,
			AbsoluteOffset = 75
		});
		await _context.SaveChangesAsync();
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldMapToFirstEntrySeasonEpisodeAndAbsolute_WhenEpisodeInPartOne()
	{
		await SeedMultiCourAsync();

		var resolution = await _service.ResolveTvdbAsync(100, 5);

		Assert.NotNull(resolution);
		Assert.Equal(1, resolution!.TvdbId);
		Assert.Equal(4, resolution.Season);
		Assert.Equal(5, resolution.Episode);
		Assert.Equal(64, resolution.AbsoluteEpisode);
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldContinueSeasonNumberingWithRestartedAbsolute_WhenEpisodeInPartTwo()
	{
		await SeedMultiCourAsync();

		var resolution = await _service.ResolveTvdbAsync(101, 1);

		Assert.NotNull(resolution);
		Assert.Equal(4, resolution!.Season);
		Assert.Equal(17, resolution.Episode);
		Assert.Equal(76, resolution.AbsoluteEpisode);
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldReturnNull_WhenEpisodeExceedsEpisodeCount()
	{
		await SeedMultiCourAsync();

		Assert.Null(await _service.ResolveTvdbAsync(100, 17));
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldReturnNull_WhenEntryUnmapped()
	{
		await SeedMultiCourAsync();

		Assert.Null(await _service.ResolveTvdbAsync(999, 1));
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldResolve_WhenEpisodeCountOpenEnded()
	{
		_context.AniListMappings.Add(new AniListMapping
		{
			AniListId = 200, TvdbId = 2, Title = "Ongoing", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = null,
			AbsoluteOffset = 0
		});
		await _context.SaveChangesAsync();

		var resolution = await _service.ResolveTvdbAsync(200, 500);

		Assert.NotNull(resolution);
		Assert.Equal(500, resolution!.Episode);
	}

	[Fact]
	public async Task ResolveAniListAsync_ShouldPickPartOne_WhenEpisodeBeforeBoundary()
	{
		await SeedMultiCourAsync();

		var resolution = await _service.ResolveAniListAsync(1, 4, 16);

		Assert.NotNull(resolution);
		Assert.Equal(100, resolution!.AniListId);
		Assert.Equal(16, resolution.AniListEpisode);
	}

	[Fact]
	public async Task ResolveAniListAsync_ShouldPickPartTwo_WhenEpisodeAtBoundary()
	{
		await SeedMultiCourAsync();

		var resolution = await _service.ResolveAniListAsync(1, 4, 17);

		Assert.NotNull(resolution);
		Assert.Equal(101, resolution!.AniListId);
		Assert.Equal(1, resolution.AniListEpisode);
	}

	[Fact]
	public async Task ResolveAniListAsync_ShouldRoundtrip_WhenResolvingBackFromTvdb()
	{
		await SeedMultiCourAsync();

		var tvdb = await _service.ResolveTvdbAsync(101, 5);
		var aniList = await _service.ResolveAniListAsync(tvdb!.TvdbId, tvdb.Season, tvdb.Episode);

		Assert.NotNull(aniList);
		Assert.Equal(101, aniList!.AniListId);
		Assert.Equal(5, aniList.AniListEpisode);
	}

	[Fact]
	public async Task ResolveAniListAsync_ShouldReturnNull_WhenEpisodePastLastEntry()
	{
		await SeedMultiCourAsync();

		Assert.Null(await _service.ResolveAniListAsync(1, 4, 29));
	}

	[Fact]
	public async Task ResolveAniListAsync_ShouldResolve_WhenEpisodeCountOpenEnded()
	{
		_context.AniListMappings.Add(new AniListMapping
		{
			AniListId = 200, TvdbId = 2, Title = "Ongoing", TvdbSeason = 1, EpisodeStart = 1, EpisodeCount = null,
			AbsoluteOffset = 0
		});
		await _context.SaveChangesAsync();

		var resolution = await _service.ResolveAniListAsync(2, 1, 500);

		Assert.NotNull(resolution);
		Assert.Equal(200, resolution!.AniListId);
		Assert.Equal(500, resolution.AniListEpisode);
	}

	[Fact]
	public async Task GetByTvdbAsync_ShouldReturnAllEntriesForSeries()
	{
		await SeedMultiCourAsync();

		var mappings = await _service.GetByTvdbAsync(1);

		Assert.Equal(2, mappings.Count);
	}

	public void Dispose()
	{
		_context.Dispose();

		// Sqlite connection pooling keeps a native handle on the file open even after disposal
		SqliteConnection.ClearAllPools();

		if (File.Exists(_dbPath))
			File.Delete(_dbPath);
	}
}
