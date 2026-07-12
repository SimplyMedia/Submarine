using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Submarine.Api.Clients;
using Submarine.Api.Jobs;
using Submarine.Api.Models.Database;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.ImportList;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

/// <summary>
///     Returns configured items instead of fetching an external source
/// </summary>
public sealed class FakeImportListFetcher : IImportListFetcher
{
	public IReadOnlyList<ImportListItem> Items { get; set; } = Array.Empty<ImportListItem>();

	public Task<IReadOnlyList<ImportListItem>> FetchAsync(ImportList list,
		CancellationToken cancellationToken = default)
		=> Task.FromResult(Items);
}

public class ImportListSyncJobTest : DatabaseTestBase
{
	[Fact]
	public async Task ExecuteAsync_ShouldAddNewAndSkipExisting_WhenListContainsBoth()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.GetTempPath(), MediaKind = MediaKind.MOVIES });
		Context.Movies.Add(new Movie { TmdbId = 100, Title = "Existing Movie" });
		Context.ImportLists.Add(new ImportList
		{
			Name = "list", Type = ImportListType.TMDB_POPULAR, Enable = true, SettingsJson = "{}",
			MediaKind = MediaKind.MOVIES, QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 1,
			Monitored = true, Tags = new List<string> { "from-list" }
		});
		await Context.SaveChangesAsync();

		var fetcher = new FakeImportListFetcher
		{
			Items = new[]
			{
				new ImportListItem(null, 100, null, "Existing Movie", false),
				new ImportListItem(null, 200, null, "New Movie", false)
			}
		};

		var metadata = new FakeMetadataClient
		{
			Movie = new MovieResource(200, null, "New Movie", null, null, null, 2026, 120, Array.Empty<string>(),
				null, null, Array.Empty<string>())
		};

		await new ImportListSyncJob().ExecuteAsync(BuildProvider(fetcher, metadata), CancellationToken.None);

		var movies = await Context.Movies.AsNoTracking().OrderBy(m => m.TmdbId).ToListAsync();
		Assert.Equal(2, movies.Count);

		var added = movies.Single(m => m.TmdbId == 200);
		Assert.Equal("New Movie", added.Title);
		Assert.True(added.Monitored);
		Assert.Contains("from-list", added.Tags);

		var version = await Context.Versions.AsNoTracking().SingleAsync(v => v.MovieId == added.Id);
		Assert.Equal("Default", version.Name);
		Assert.Equal(1, version.QualityProfileId);
		Assert.Equal(1, version.LanguageProfileId);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotFetch_WhenListIsDisabled()
	{
		Context.ImportLists.Add(new ImportList
		{
			Name = "disabled", Type = ImportListType.TMDB_POPULAR, Enable = false, SettingsJson = "{}",
			MediaKind = MediaKind.MOVIES, QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 1
		});
		await Context.SaveChangesAsync();

		var fetcher = new FakeImportListFetcher
		{
			Items = new[] { new ImportListItem(null, 200, null, "New Movie", false) }
		};

		await new ImportListSyncJob().ExecuteAsync(BuildProvider(fetcher, new FakeMetadataClient()),
			CancellationToken.None);

		Assert.Empty(await Context.Movies.AsNoTracking().ToListAsync());
	}

	private IServiceProvider BuildProvider(IImportListFetcher fetcher, IMetadataClient metadata)
	{
		var services = new ServiceCollection();

		services.AddSingleton<SubmarineDatabaseContext>(Context);
		services.AddSingleton(fetcher);
		services.AddSingleton(metadata);
		services.AddSingleton(new SeriesService(new SeriesRepository(Context), new RootFolderRepository(Context),
			metadata));
		services.AddSingleton(new MovieService(new MovieRepository(Context), new RootFolderRepository(Context),
			metadata));
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		return services.BuildServiceProvider();
	}
}
