using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;
using MetadataSeriesStatus = Submarine.Metadata.Contracts.SeriesStatus;

namespace Submarine.Api.Tests;

public class MonitorAndSearchOnAddTest : DatabaseTestBase
{
	[Theory]
	[InlineData(MonitorOption.ALL, new[] { 101, 102, 201 })]
	[InlineData(MonitorOption.FUTURE, new[] { 201 })]
	[InlineData(MonitorOption.PILOT, new[] { 101 })]
	[InlineData(MonitorOption.LATEST_SEASON, new[] { 201 })]
	[InlineData(MonitorOption.NONE, new int[0])]
	public async Task AddAsync_ShouldMonitorExpectedEpisodes_WhenMonitorOptionGiven(MonitorOption option,
		int[] expectedKeys)
	{
		await SeedRootFoldersAsync();
		var service = BuildSeriesService(out _);

		var series = await service.AddAsync(new AddSeriesRequest
		{
			TvdbId = 42, RootFolderId = 1, QualityProfileId = 1, LanguageProfileId = 1, Monitor = option
		});

		var monitored = (await Context.Episodes.AsNoTracking().Where(e => e.SeriesId == series.Id).ToListAsync())
			.Where(e => e.Monitored)
			.Select(e => e.SeasonNumber * 100 + e.EpisodeNumber)
			.OrderBy(k => k)
			.ToArray();

		Assert.Equal(expectedKeys, monitored);
	}

	[Fact]
	public async Task AddAsync_ShouldEnqueueSeasonSearchesPerMonitoredSeason_WhenSearchOnAdd()
	{
		await SeedRootFoldersAsync();
		var service = BuildSeriesService(out var queue);

		await service.AddAsync(new AddSeriesRequest
		{
			TvdbId = 42, RootFolderId = 1, QualityProfileId = 1, LanguageProfileId = 1, SearchOnAdd = true
		});

		Assert.Equal(2, queue.Items.Count);
	}

	[Fact]
	public async Task AddAsync_ShouldNotEnqueue_WhenSearchOnAddDefaultFalse()
	{
		await SeedRootFoldersAsync();
		var service = BuildSeriesService(out var queue);

		await service.AddAsync(new AddSeriesRequest
		{
			TvdbId = 42, RootFolderId = 1, QualityProfileId = 1, LanguageProfileId = 1
		});

		Assert.Empty(queue.Items);
	}

	[Fact]
	public async Task AddAsync_ShouldEnqueueMovieSearch_WhenSearchOnAdd()
	{
		await SeedRootFoldersAsync();
		var queue = new FakeBackgroundTaskQueue();
		var service = new MovieService(new MovieRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			MovieMetadata(), queue, new VersionService(Context));

		await service.AddAsync(new AddMovieRequest
		{
			TmdbId = 5, RootFolderId = 2, QualityProfileId = 1, LanguageProfileId = 1, SearchOnAdd = true
		});

		Assert.Single(queue.Items);
	}

	[Fact]
	public async Task AddAsync_ShouldNotEnqueueMovieSearch_WhenSearchOnAddDefaultFalse()
	{
		await SeedRootFoldersAsync();
		var queue = new FakeBackgroundTaskQueue();
		var service = new MovieService(new MovieRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			MovieMetadata(), queue, new VersionService(Context));

		await service.AddAsync(new AddMovieRequest
		{
			TmdbId = 5, RootFolderId = 2, QualityProfileId = 1, LanguageProfileId = 1
		});

		Assert.Empty(queue.Items);
	}

	private async Task SeedRootFoldersAsync()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "series"), MediaKind = MediaKind.SERIES });
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "movies"), MediaKind = MediaKind.MOVIES });
		await Context.SaveChangesAsync();
	}

	private SeriesService BuildSeriesService(out FakeBackgroundTaskQueue queue)
	{
		queue = new FakeBackgroundTaskQueue();

		return new SeriesService(new SeriesRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			new FakeMetadataClient { Series = SeriesResourceWithEpisodes() }, queue, new VersionService(Context));
	}

	private static FakeMetadataClient MovieMetadata()
		=> new()
		{
			Movie = new MovieResource(5, null, "Film", null, null, null, 2021, 120, Array.Empty<string>(), null, null,
				Array.Empty<string>())
		};

	private static SeriesResource SeriesResourceWithEpisodes()
	{
		var past = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
		var future = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

		return new SeriesResource(42, null, "Show", null, null, null, MetadataSeriesStatus.Continuing, 30, null,
			Array.Empty<string>(),
			new[]
			{
				new SeasonResource(0, null, 1), new SeasonResource(1, null, 2), new SeasonResource(2, null, 1)
			},
			new[]
			{
				Episode(0, 1, past), Episode(1, 1, past), Episode(1, 2, past), Episode(2, 1, future)
			},
			null, 2020);
	}

	private static EpisodeResource Episode(int season, int number, DateOnly air)
		=> new(season * 100 + number, null, $"E{number}", null, air, 30,
			new[] { new EpisodeNumber(EpisodeOrdering.Aired, season, number, null) });
}
