using Microsoft.EntityFrameworkCore;
using Submarine.Api.Exceptions;
using Submarine.Api.Models.Request;
using Submarine.Api.Repository;
using Submarine.Api.Services;
using Submarine.Core.Library;
using Xunit;

namespace Submarine.Api.Tests;

public class MovieServiceTest : DatabaseTestBase
{
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
		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();

		var service = BuildService();

		var updated = await service.EditorAsync(new MovieEditorRequest
		{
			MovieIds = new List<int> { movie1.Id, movie2.Id },
			MinimumAvailability = MinimumAvailability.RELEASED
		});

		Assert.Equal(2, updated);

		var movies = await Context.Movies.AsNoTracking().ToListAsync();
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
		await Context.SaveChangesAsync();
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
			new FakeMetadataClient(), new FakeBackgroundTaskQueue(), new VersionService(Context));
}
