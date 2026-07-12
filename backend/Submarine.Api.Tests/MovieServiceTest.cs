using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Api.Tests;

public class MovieServiceTest : DatabaseTestBase
{
	[Fact]
	public async Task AddAsync_ShouldPopulateCollectionFields_WhenResourceBelongsToCollection()
	{
		Context.RootFolders.Add(new RootFolder { Path = Path.Combine("root", "movies"), MediaKind = MediaKind.MOVIES });
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

		var metadataClient = new FakeMetadataClient
		{
			Movie = new MovieResource(5, null, "Film", null, null, null, 2021, 120, Array.Empty<string>(), null, null,
				Array.Empty<string>(), 10, "Collection A")
		};

		var service = new MovieService(new MovieRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			metadataClient, new FakeBackgroundTaskQueue(), new VersionService(Context), new FakeEventPublisher());

		var movie = await service.AddAsync(new AddMovieRequest
		{
			TmdbId = 5, RootFolderId = 1, QualityProfileId = 1, LanguageProfileId = 1
		});

		Assert.Equal(10, movie.TmdbCollectionId);
		Assert.Equal("Collection A", movie.CollectionTitle);
	}

	[Fact]
	public async Task EditorAsync_ShouldApplyMinimumAvailabilityToAllGivenMovies()
	{
		var movie1 = new Movie
		{
			TmdbId = 1, Title = "Movie 1", MinimumAvailability = MinimumAvailability.ANNOUNCED,
			Versions = new List<MediaVersion>
				{ new() { Name = "Default", Path = "/lib/m1", QualityProfileId = 1, LanguageProfileId = 1 } }
		};
		var movie2 = new Movie
		{
			TmdbId = 2, Title = "Movie 2", MinimumAvailability = MinimumAvailability.ANNOUNCED,
			Versions = new List<MediaVersion>
				{ new() { Name = "Default", Path = "/lib/m2", QualityProfileId = 1, LanguageProfileId = 1 } }
		};
		Context.Movies.AddRange(movie1, movie2);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
		Context.ChangeTracker.Clear();

		var service = BuildService();

		var updated = await service.EditorAsync(new MovieEditorRequest
		{
			MovieIds = new List<int> { movie1.Id, movie2.Id },
			MinimumAvailability = MinimumAvailability.RELEASED
		});

		Assert.Equal(2, updated);

		var movies = await Context.Movies.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
		Assert.All(movies, m => Assert.Equal(MinimumAvailability.RELEASED, m.MinimumAvailability));
	}

	[Fact]
	public async Task EditorAsync_ShouldThrow_WhenQualityProfileDoesNotExist()
	{
		var movie = new Movie
		{
			TmdbId = 1, Title = "Movie 1",
			Versions = new List<MediaVersion>
				{ new() { Name = "Default", Path = "/lib/m1", QualityProfileId = 1, LanguageProfileId = 1 } }
		};
		Context.Movies.Add(movie);
		await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
		Context.ChangeTracker.Clear();

		var service = BuildService();

		await Assert.ThrowsAsync<BadRequestException>(() => service.EditorAsync(new MovieEditorRequest
		{
			MovieIds = new List<int> { movie.Id }, QualityProfileId = 999
		}));
	}

	private MovieService BuildService()
		=> new(new MovieRepository(Context), new RootFolderRepository(Context),
			new QualityProfileRepository(Context), new LanguageProfileRepository(Context),
			new FakeMetadataClient(), new FakeBackgroundTaskQueue(), new VersionService(Context), new FakeEventPublisher());
}
