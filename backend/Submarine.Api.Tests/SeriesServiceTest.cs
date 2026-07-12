using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Languages;
using Submarine.Core.Library;
using Submarine.Core.Profile;
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

	[Fact]
	public async Task SetSeasonMonitoredAsync_ShouldCascadeToEpisodesOfSeason()
	{
		var series = new Series { TvdbId = 1, Title = "Show" };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		Context.Seasons.Add(new Season { SeriesId = series.Id, SeasonNumber = 1, Monitored = false });
		var seasonTwoEpisode = new Episode
			{ SeriesId = series.Id, SeasonNumber = 2, EpisodeNumber = 1, Monitored = true };
		var seasonOneEpisode1 = new Episode
			{ SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = false };
		var seasonOneEpisode2 = new Episode
			{ SeriesId = series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = false };
		Context.Episodes.AddRange(seasonOneEpisode1, seasonOneEpisode2, seasonTwoEpisode);
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var service = BuildService(new FakeMetadataClient());

		await service.SetSeasonMonitoredAsync(series.Id, seasonNumber: 1, monitored: true);

		var season = await Context.Seasons.AsNoTracking().SingleAsync(s => s.SeasonNumber == 1);
		Assert.True(season.Monitored);

		var episodes = await Context.Episodes.AsNoTracking().ToListAsync();
		Assert.True(episodes.Single(e => e.Id == seasonOneEpisode1.Id).Monitored);
		Assert.True(episodes.Single(e => e.Id == seasonOneEpisode2.Id).Monitored);
		Assert.True(episodes.Single(e => e.Id == seasonTwoEpisode.Id).Monitored);
	}

	[Fact]
	public async Task EditorAsync_ShouldApplyProfilesToDefaultVersionAndUpdateTags()
	{
		var series = new Series { TvdbId = 1, Title = "Show", Tags = new List<string> { "keep", "drop" } };
		Context.Series.Add(series);
		await Context.SaveChangesAsync();

		var defaultVersion = new MediaVersion
		{
			SeriesId = series.Id, Name = "Default", Path = "/lib/Show", QualityProfileId = 1, LanguageProfileId = 1
		};
		var extraVersion = new MediaVersion
		{
			SeriesId = series.Id, Name = "4K", Path = "/lib/Show4K", QualityProfileId = 1, LanguageProfileId = 1
		};
		Context.Versions.AddRange(defaultVersion, extraVersion);
		Context.QualityProfiles.Add(new QualityProfile { Id = 2, Name = "HD", UpgradeAllowed = false, Cutoff = 0 });
		Context.LanguageProfiles.Add(new LanguageProfile
		{
			Id = 2, Name = "French", Languages = new List<Language> { Language.FRENCH }, Cutoff = Language.FRENCH,
			UpgradeAllowed = false
		});
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var service = BuildService(new FakeMetadataClient());

		var updated = await service.EditorAsync(new SeriesEditorRequest
		{
			SeriesIds = new List<int> { series.Id },
			QualityProfileId = 2,
			LanguageProfileId = 2,
			AddTags = new List<string> { "new" },
			RemoveTags = new List<string> { "drop" }
		});

		Assert.Equal(1, updated);

		var versions = await Context.Versions.AsNoTracking().OrderBy(v => v.Id).ToListAsync();
		Assert.Equal(2, versions[0].QualityProfileId);
		Assert.Equal(2, versions[0].LanguageProfileId);
		Assert.Equal(1, versions[1].QualityProfileId);
		Assert.Equal(1, versions[1].LanguageProfileId);

		var updatedSeries = await Context.Series.AsNoTracking().SingleAsync(s => s.Id == series.Id);
		Assert.Equal(new List<string> { "keep", "new" }, updatedSeries.Tags);
	}

	private SeriesService BuildService(FakeMetadataClient metadata, FakeBackgroundTaskQueue? queue = null)
		=> new(new SeriesRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context), metadata,
			queue ?? new FakeBackgroundTaskQueue(), new VersionService(Context));

	private static SeriesResource Resource()
		=> new(42, null, "Show", null, null, null, Submarine.Metadata.Contracts.SeriesStatus.Continuing, 30, null,
			Array.Empty<string>(), Array.Empty<SeasonResource>(), Array.Empty<EpisodeResource>(), null, 2020);
}
