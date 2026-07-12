using Microsoft.EntityFrameworkCore;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class CollectionServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task GetAllAsync_ShouldGroupSeededMoviesByCollection()
	{
		Context.Movies.AddRange(
			new Movie
			{
				TmdbId = 1, Title = "Movie 1", TmdbCollectionId = 10, CollectionTitle = "Collection A",
				Versions = new List<MediaVersion>
					{ new() { Name = "Default", Path = "/lib/m1", QualityProfileId = 1, LanguageProfileId = 1 } }
			},
			new Movie
			{
				TmdbId = 2, Title = "Movie 2", TmdbCollectionId = 10, CollectionTitle = "Collection A",
				Versions = new List<MediaVersion>
					{ new() { Name = "Default", Path = "/lib/m2", QualityProfileId = 1, LanguageProfileId = 1 } }
			},
			new Movie
			{
				TmdbId = 3, Title = "Movie 3",
				Versions = new List<MediaVersion>
					{ new() { Name = "Default", Path = "/lib/m3", QualityProfileId = 1, LanguageProfileId = 1 } }
			});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
		Context.ChangeTracker.Clear();

		var service = BuildService(new FakeMetadataClient());

		var collections = await service.GetAllAsync();

		var collection = Assert.Single(collections);
		Assert.Equal(10, collection.TmdbCollectionId);
		Assert.Equal("Collection A", collection.Title);
		Assert.Equal(2, collection.MovieCount);
	}

	[Fact]
	public async Task AddMissingAsync_ShouldSkipExisting_AndAddOnlyMissingMovies()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "movies"), MediaKind = MediaKind.MOVIES });

		Context.Movies.Add(new Movie
		{
			TmdbId = 1, Title = "Existing Movie", TmdbCollectionId = 10, CollectionTitle = "Collection A",
			Versions = new List<MediaVersion>
				{ new() { Name = "Default", Path = "/lib/existing", QualityProfileId = 1, LanguageProfileId = 1 } }
		});
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
		Context.ChangeTracker.Clear();

		var newMovieResource = new MovieResource(2, null, "New Movie", null, null, null, 2021, 110,
			Array.Empty<string>(), null, null, Array.Empty<string>(), 10, "Collection A");

		var metadataClient = new FakeMetadataClient
		{
			Collection = new CollectionResource(10, "Collection A", null, new[]
			{
				new MovieResource(1, null, "Existing Movie", null, null, null, 2020, 100, Array.Empty<string>(),
					null, null, Array.Empty<string>(), 10, "Collection A"),
				newMovieResource
			}),
			Movie = newMovieResource
		};

		var service = BuildService(metadataClient);

		var result = await service.AddMissingAsync(10, new AddCollectionMoviesRequest
		{
			QualityProfileId = 1, LanguageProfileId = 1, RootFolderId = 1
		}, TestContext.Current.CancellationToken);

		var added = Assert.Single(result.Added);
		Assert.Equal(2, added.TmdbId);
		Assert.Empty(result.Failed);

		var moviesInDb = await Context.Movies.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
		Assert.Equal(2, moviesInDb.Count);
	}

	private CollectionService BuildService(FakeMetadataClient metadataClient)
	{
		var movieService = new MovieService(new MovieRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			metadataClient, new FakeBackgroundTaskQueue(), new VersionService(Context), new FakeEventPublisher());

		return new CollectionService(new MovieRepository(Context), metadataClient, movieService);
	}
}
