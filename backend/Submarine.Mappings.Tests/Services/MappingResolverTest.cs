using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Submarine.Mappings.Database;
using Submarine.Mappings.Models;
using Submarine.Mappings.Services;
using Xunit;

namespace Submarine.Mappings.Tests.Services;

public class MappingResolverTest : IDisposable
{
	private readonly string _dbPath;
	private readonly MappingsDatabaseContext _context;
	private readonly MappingResolver _resolver;

	public MappingResolverTest()
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

		_resolver = new MappingResolver(_context);
	}

	[Fact]
	public async Task ResolveSceneAsync_ShouldApplySeasonOffset_WhenNoEpisodeOverrideExists()
	{
		_context.SceneMappings.Add(new SceneMapping
		{
			TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 1
		});
		await _context.SaveChangesAsync();

		var (season, episode) = await _resolver.ResolveSceneAsync(1, 1, 5);

		Assert.Equal(1, season);
		Assert.Equal(6, episode);
	}

	[Fact]
	public async Task ResolveSceneAsync_ShouldPreferEpisodeOverride_WhenOverrideAndSeasonOffsetBothExist()
	{
		_context.SceneMappings.Add(new SceneMapping
		{
			TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 1
		});
		_context.SceneEpisodeMappings.Add(new SceneEpisodeMapping
		{
			TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 5, SceneSeasonNumber = 2, SceneEpisodeNumber = 99
		});
		await _context.SaveChangesAsync();

		var (season, episode) = await _resolver.ResolveSceneAsync(1, 1, 5);

		Assert.Equal(2, season);
		Assert.Equal(99, episode);
	}

	[Fact]
	public async Task ResolveSceneAsync_ShouldReturnUnchanged_WhenNoMappingExists()
	{
		var (season, episode) = await _resolver.ResolveSceneAsync(404, 1, 5);

		Assert.Equal(1, season);
		Assert.Equal(5, episode);
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldRoundtrip_WhenResolvingBackFromSceneNumbering()
	{
		_context.SceneMappings.Add(new SceneMapping
		{
			TvdbId = 1, Title = "Show", SeasonNumber = 1, SceneSeasonNumber = 1, EpisodeOffset = 1
		});
		await _context.SaveChangesAsync();

		var (sceneSeason, sceneEpisode) = await _resolver.ResolveSceneAsync(1, 1, 5);
		var (season, episode) = await _resolver.ResolveTvdbAsync(1, sceneSeason, sceneEpisode);

		Assert.Equal(1, season);
		Assert.Equal(5, episode);
	}

	[Fact]
	public async Task ResolveTvdbAsync_ShouldRoundtrip_WhenEpisodeOverrideApplies()
	{
		_context.SceneEpisodeMappings.Add(new SceneEpisodeMapping
		{
			TvdbId = 1, SeasonNumber = 1, EpisodeNumber = 5, SceneSeasonNumber = 2, SceneEpisodeNumber = 99
		});
		await _context.SaveChangesAsync();

		var (sceneSeason, sceneEpisode) = await _resolver.ResolveSceneAsync(1, 1, 5);
		var (season, episode) = await _resolver.ResolveTvdbAsync(1, sceneSeason, sceneEpisode);

		Assert.Equal(1, season);
		Assert.Equal(5, episode);
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
