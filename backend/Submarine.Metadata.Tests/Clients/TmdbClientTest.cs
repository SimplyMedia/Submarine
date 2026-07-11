using Microsoft.Extensions.Configuration;
using Submarine.Metadata.Clients;
using Xunit;

namespace Submarine.Metadata.Tests.Clients;

public class TmdbClientTest
{
	[Fact]
	public async Task GetMovieAsync_ShouldNormalizeMovie_WhenTmdbReturnsFullPayload()
	{
		const string json = """
			{
				"id": 603,
				"imdb_id": "tt0133093",
				"title": "The Matrix",
				"overview": "Set in the 22nd century, The Matrix tells the story of a computer hacker.",
				"release_date": "1999-03-30",
				"runtime": 136,
				"genres": [ { "id": 28, "name": "Action" }, { "id": 878, "name": "Science Fiction" } ],
				"production_companies": [ { "id": 79, "name": "Village Roadshow Pictures" } ],
				"poster_path": "/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg"
			}
			""";

		var client = CreateClient(json);

		var movie = await client.GetMovieAsync(603);

		Assert.NotNull(movie);
		Assert.Equal(603, movie.TmdbId);
		Assert.Equal("tt0133093", movie.ImdbId);
		Assert.Equal("The Matrix", movie.Title);
		Assert.Equal(new DateOnly(1999, 3, 30), movie.ReleaseDate);
		Assert.Equal(1999, movie.Year);
		Assert.Equal(136, movie.Runtime);
		Assert.Contains("Action", movie.Genres);
		Assert.Contains("Science Fiction", movie.Genres);
		Assert.Equal("Village Roadshow Pictures", movie.Studio);
		Assert.Equal("https://image.tmdb.org/t/p/original/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg", movie.ImageUrl);
	}

	[Fact]
	public async Task GetMovieAsync_ShouldNormalizeMinimalMovie_WhenTmdbReturnsMinimalPayload()
	{
		const string json = """
			{
				"id": 42,
				"title": "Untitled"
			}
			""";

		var client = CreateClient(json);

		var movie = await client.GetMovieAsync(42);

		Assert.NotNull(movie);
		Assert.Equal(42, movie.TmdbId);
		Assert.Equal("Untitled", movie.Title);
		Assert.Null(movie.ReleaseDate);
		Assert.Null(movie.Year);
		Assert.Null(movie.Studio);
		Assert.Null(movie.ImageUrl);
		Assert.Empty(movie.Genres);
	}

	[Fact]
	public async Task SearchMoviesAsync_ShouldReturnNormalizedResults_WhenTmdbReturnsSearchPayload()
	{
		const string json = """
			{
				"page": 1,
				"results": [
					{
						"id": 603,
						"title": "The Matrix",
						"release_date": "1999-03-30"
					}
				],
				"total_results": 1,
				"total_pages": 1
			}
			""";

		var client = CreateClient(json);

		var results = await client.SearchMoviesAsync("matrix");

		var result = Assert.Single(results);
		Assert.Equal(603, result.TmdbId);
		Assert.Equal("The Matrix", result.Title);
		Assert.Equal(1999, result.Year);
	}

	private static TmdbClient CreateClient(string responseJson)
	{
		var httpClient = new HttpClient(new StubHttpMessageHandler(responseJson))
		{
			BaseAddress = new Uri("https://api.themoviedb.org/3/")
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Tmdb:ApiKey"] = "test-key" })
			.Build();

		return new TmdbClient(httpClient, configuration);
	}
}
