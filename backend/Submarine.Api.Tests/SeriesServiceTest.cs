using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class SeriesServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task AddAsync_ShouldCreateDefaultAndExtraVersionWithDistinctPaths_WhenExtraVersionRequested()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "hd"), MediaKind = MediaKind.SERIES });
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "uhd"), MediaKind = MediaKind.SERIES });
		await Context.SaveChangesAsync();

		var service = BuildService(new FakeMetadataClient { Series = Resource() });

		var series = await service.AddAsync(new AddSeriesRequest
		{
			TvdbId = 42,
			RootFolderId = 1,
			QualityProfileId = 1,
			LanguageProfileId = 1,
			Versions = new List<AddMediaVersionRequest>
			{
				new() { Name = "4K", QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 2 }
			}
		});

		var versions = await Context.Versions.AsNoTracking().Where(v => v.SeriesId == series.Id)
			.OrderBy(v => v.Id).ToListAsync();

		Assert.Equal(2, versions.Count);
		Assert.Equal(new[] { "Default", "4K" }, versions.Select(v => v.Name));
		Assert.Equal(2, versions.Select(v => v.Path).Distinct().Count());
	}

	[Fact]
	public async Task AddAsync_ShouldThrow_WhenTwoVersionsResolveToSamePath()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "hd"), MediaKind = MediaKind.SERIES });
		await Context.SaveChangesAsync();

		var service = BuildService(new FakeMetadataClient { Series = Resource() });

		await Assert.ThrowsAsync<BadRequestException>(() => service.AddAsync(new AddSeriesRequest
		{
			TvdbId = 42,
			RootFolderId = 1,
			QualityProfileId = 1,
			LanguageProfileId = 1,
			Versions = new List<AddMediaVersionRequest>
			{
				new() { Name = "Duplicate", QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 1 }
			}
		}));
	}

	[Fact]
	public async Task UpdateAsync_ShouldEnqueueRefresh_WhenNumberingChanges()
	{
		Context.Series.Add(new Series
		{
			TvdbId = 7, Title = "Show", MetadataProvider = MetadataProvider.TVDB, Numbering = EpisodeNumbering.AIRED
		});
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var queue = new FakeBackgroundTaskQueue();
		var service = BuildService(new FakeMetadataClient(), queue);

		await service.UpdateAsync(1, new UpdateSeriesRequest { Numbering = EpisodeNumbering.DVD });

		Assert.Single(queue.Items);
		Assert.Equal(EpisodeNumbering.DVD,
			(await Context.Series.AsNoTracking().FirstAsync(s => s.Id == 1)).Numbering);
	}

	[Fact]
	public async Task UpdateAsync_ShouldNotEnqueueRefresh_WhenNumberingUnchanged()
	{
		Context.Series.Add(new Series { TvdbId = 7, Title = "Show", Numbering = EpisodeNumbering.AIRED });
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var queue = new FakeBackgroundTaskQueue();
		var service = BuildService(new FakeMetadataClient(), queue);

		await service.UpdateAsync(1, new UpdateSeriesRequest { Numbering = EpisodeNumbering.AIRED, Monitored = false });

		Assert.Empty(queue.Items);
	}

	private SeriesService BuildService(FakeMetadataClient metadata, FakeBackgroundTaskQueue? queue = null)
		=> new(new SeriesRepository(Context), new RootFolderRepository(Context), metadata,
			queue ?? new FakeBackgroundTaskQueue(), new VersionService(Context));

	private static SeriesResource Resource()
		=> new(42, null, "Show", null, null, null, Submarine.Metadata.Contracts.SeriesStatus.Continuing, 30, null,
			Array.Empty<string>(), Array.Empty<SeasonResource>(), Array.Empty<EpisodeResource>(), null, 2020);
}
