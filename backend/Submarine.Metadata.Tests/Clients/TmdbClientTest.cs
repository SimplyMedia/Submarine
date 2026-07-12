using System.Net;
using Microsoft.Extensions.Configuration;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;
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

	[Fact]
	public async Task GetSeriesAsync_ShouldNormalizeMultiSeasonSeries_WhenTmdbReturnsExternalIdsAndSpecials()
	{
		const string detailJson = """
			{
				"id": 1399,
				"name": "Game of Thrones",
				"overview": "Seven noble families fight for control of Westeros.",
				"status": "Ended",
				"first_air_date": "2011-04-17",
				"episode_run_time": [ 60 ],
				"networks": [ { "id": 49, "name": "HBO" } ],
				"genres": [ { "id": 18, "name": "Drama" }, { "id": 10765, "name": "Sci-Fi & Fantasy" } ],
				"poster_path": "/poster.jpg",
				"seasons": [
					{ "season_number": 0, "name": "Specials", "episode_count": 2 },
					{ "season_number": 1, "name": "Season 1", "episode_count": 2 },
					{ "season_number": 2, "name": "Season 2", "episode_count": 1 }
				],
				"external_ids": { "tvdb_id": 121361 }
			}
			""";

		const string season0Json = """
			{ "episodes": [
				{ "id": 100, "name": "Special A", "overview": "s", "air_date": "2011-01-01", "runtime": 30, "season_number": 0, "episode_number": 1 },
				{ "id": 101, "name": "Special B", "air_date": "2011-01-02", "season_number": 0, "episode_number": 2 }
			] }
			""";

		const string season1Json = """
			{ "episodes": [
				{ "id": 1, "name": "Winter Is Coming", "overview": "o", "air_date": "2011-04-17", "runtime": 62, "season_number": 1, "episode_number": 1 },
				{ "id": 2, "name": "The Kingsroad", "air_date": "2011-04-24", "season_number": 1, "episode_number": 2 }
			] }
			""";

		const string season2Json = """{ "episodes": [ { "id": 21, "name": "The North Remembers", "air_date": "2012-04-01", "season_number": 2, "episode_number": 1 } ] }""";

		var client = CreateClient(detailJson, season0Json, season1Json, season2Json);

		var series = await client.GetSeriesAsync(1399);

		Assert.NotNull(series);
		Assert.Equal(1399, series.TmdbId);
		Assert.Equal(121361, series.TvdbId);
		Assert.Equal("Game of Thrones", series.Title);
		Assert.Equal(SeriesStatus.Ended, series.Status);
		Assert.Equal(new DateOnly(2011, 4, 17), series.FirstAired);
		Assert.Equal(2011, series.Year);
		Assert.Equal(60, series.Runtime);
		Assert.Equal("HBO", series.Network);
		Assert.Contains("Drama", series.Genres);
		Assert.Contains("Sci-Fi & Fantasy", series.Genres);
		Assert.Equal("https://image.tmdb.org/t/p/original/poster.jpg", series.ImageUrl);

		Assert.Equal(3, series.Seasons.Count);
		Assert.Equal(0, series.Seasons[0].SeasonNumber);
		Assert.Equal("Specials", series.Seasons[0].Name);

		Assert.Equal(5, series.Episodes.Count);
		var special = series.Episodes[0];
		Assert.Equal(100, special.TmdbId);
		Assert.Null(special.TvdbId);
		var number = Assert.Single(special.Numbers);
		Assert.Equal(new EpisodeNumber(EpisodeOrdering.Aired, 0, 1, null), number);
		Assert.Contains(series.Episodes, e => e.Numbers[0] == new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null));
	}

	[Fact]
	public async Task GetSeriesAsync_ShouldDefaultTvdbIdToZero_WhenExternalIdsAbsent()
	{
		const string detailJson = """
			{
				"id": 555,
				"name": "Solo Show",
				"status": "Returning Series",
				"first_air_date": "2020-01-01",
				"seasons": [ { "season_number": 1, "name": "Season 1", "episode_count": 1 } ]
			}
			""";

		const string season1Json = """{ "episodes": [ { "id": 9, "name": "Ep", "season_number": 1, "episode_number": 1 } ] }""";

		var client = CreateClient(detailJson, season1Json);

		var series = await client.GetSeriesAsync(555);

		Assert.NotNull(series);
		Assert.Equal(555, series.TmdbId);
		Assert.Equal(0, series.TvdbId);
		Assert.Equal(SeriesStatus.Continuing, series.Status);
		Assert.Null(series.Runtime);
		var episode = Assert.Single(series.Episodes);
		Assert.Equal(9, episode.TmdbId);
	}

	[Fact]
	public async Task SearchSeriesAsync_ShouldReturnNormalizedResults_WhenTmdbReturnsSearchPayload()
	{
		const string json = """
			{
				"page": 1,
				"results": [
					{
						"id": 1399,
						"name": "Game of Thrones",
						"overview": "o",
						"first_air_date": "2011-04-17",
						"poster_path": "/p.jpg"
					}
				]
			}
			""";

		var client = CreateClient(json);

		var results = await client.SearchSeriesAsync("game of thrones");

		var result = Assert.Single(results);
		Assert.Equal(1399, result.TmdbId);
		Assert.Equal(0, result.TvdbId);
		Assert.Equal("Game of Thrones", result.Title);
		Assert.Equal(2011, result.Year);
		Assert.Equal("https://image.tmdb.org/t/p/original/p.jpg", result.ImageUrl);
		Assert.Empty(result.Episodes);
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

	private static TmdbClient CreateClient(params string[] responses)
	{
		var handler = new StubHttpMessageHandler(responses.Select(r => (HttpStatusCode.OK, r)).ToArray());
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://api.themoviedb.org/3/")
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Tmdb:ApiKey"] = "test-key" })
			.Build();

		return new TmdbClient(httpClient, configuration);
	}
}
