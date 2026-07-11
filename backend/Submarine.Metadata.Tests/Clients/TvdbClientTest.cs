using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Submarine.Metadata.Clients;
using Submarine.Metadata.Contracts;
using Xunit;

namespace Submarine.Metadata.Tests.Clients;

public class TvdbClientTest
{
	private const string LoginJson = """{ "status": "success", "data": { "token": "token-1" } }""";

	private const string SeriesJson = """
		{
			"status": "success",
			"data": {
				"id": 81797,
				"name": "One Piece",
				"overview": "Monkey D. Luffy sets off on a journey to find the legendary One Piece.",
				"firstAired": "1999-10-20",
				"averageRuntime": 25,
				"status": { "name": "Continuing" },
				"originalNetwork": { "name": "Fuji TV" },
				"genres": [ { "name": "Animation" }, { "name": "Adventure" } ],
				"image": "https://artworks.thetvdb.com/banners/posters/81797-1.jpg",
				"seasons": [
					{ "number": 1, "name": "East Blue", "type": { "type": "official" } },
					{ "number": 1, "type": { "type": "dvd" } }
				],
				"episodes": [
					{
						"id": 184061,
						"name": "I'm Luffy! The Man Who Will Become the Pirate King!",
						"overview": "Luffy sets sail.",
						"aired": "1999-10-20",
						"runtime": 25,
						"seasonNumber": 1,
						"number": 1,
						"absoluteNumber": 1,
						"dvdSeason": 1,
						"dvdEpisode": 1
					},
					{
						"id": 184062,
						"name": "The Great Swordsman Appears!",
						"aired": "1999-11-17",
						"seasonNumber": 1,
						"number": 2
					}
				]
			}
		}
		""";

	[Fact]
	public async Task GetSeriesAsync_ShouldNormalizeSeries_WhenTvdbReturnsExtendedPayload()
	{
		var (client, handler) = CreateClient((HttpStatusCode.OK, LoginJson), (HttpStatusCode.OK, SeriesJson));

		var series = await client.GetSeriesAsync(81797);

		Assert.NotNull(series);
		Assert.Equal(81797, series.TvdbId);
		Assert.Equal("One Piece", series.Title);
		Assert.Equal("Monkey D. Luffy sets off on a journey to find the legendary One Piece.", series.Overview);
		Assert.Equal(SeriesStatus.Continuing, series.Status);
		Assert.Equal(new DateOnly(1999, 10, 20), series.FirstAired);
		Assert.Equal(1999, series.Year);
		Assert.Equal("Fuji TV", series.Network);
		Assert.Equal(25, series.Runtime);
		Assert.Contains("Animation", series.Genres);
		Assert.Contains("Adventure", series.Genres);
		Assert.Equal("https://artworks.thetvdb.com/banners/posters/81797-1.jpg", series.ImageUrl);

		var season = Assert.Single(series.Seasons);
		Assert.Equal(1, season.SeasonNumber);
		Assert.Equal("East Blue", season.Name);
		Assert.Equal(2, season.EpisodeCount);

		Assert.Equal(2, series.Episodes.Count);
		var first = series.Episodes[0];
		Assert.Equal(184061, first.TvdbId);
		Assert.Equal(new DateOnly(1999, 10, 20), first.AirDate);
		Assert.Equal(3, first.Numbers.Count);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Aired, 1, 1, null), first.Numbers);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Dvd, 1, 1, null), first.Numbers);
		Assert.Contains(new EpisodeNumber(EpisodeOrdering.Absolute, null, null, 1), first.Numbers);

		var second = series.Episodes[1];
		var airedOnly = Assert.Single(second.Numbers);
		Assert.Equal(new EpisodeNumber(EpisodeOrdering.Aired, 1, 2, null), airedOnly);

		Assert.Equal(2, handler.Requests.Count);
		Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
		Assert.EndsWith("/login", handler.Requests[0].RequestUri!.AbsolutePath);
		Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization?.Scheme);
		Assert.Equal("token-1", handler.Requests[1].Headers.Authorization?.Parameter);
	}

	[Fact]
	public async Task GetSeriesAsync_ShouldReuseCachedToken_WhenCalledTwice()
	{
		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, LoginJson),
			(HttpStatusCode.OK, SeriesJson),
			(HttpStatusCode.OK, SeriesJson));

		await client.GetSeriesAsync(81797);
		await client.GetSeriesAsync(81797);

		Assert.Equal(3, handler.Requests.Count);
		Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
		Assert.Equal("token-1", handler.Requests[2].Headers.Authorization?.Parameter);
	}

	[Fact]
	public async Task GetSeriesAsync_ShouldLoginAgainAndRetryOnce_WhenTvdbReturnsUnauthorized()
	{
		const string secondLoginJson = """{ "status": "success", "data": { "token": "token-2" } }""";

		var (client, handler) = CreateClient(
			(HttpStatusCode.OK, LoginJson),
			(HttpStatusCode.Unauthorized, "{}"),
			(HttpStatusCode.OK, secondLoginJson),
			(HttpStatusCode.OK, SeriesJson));

		var series = await client.GetSeriesAsync(81797);

		Assert.NotNull(series);
		Assert.Equal(4, handler.Requests.Count);
		Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
		Assert.Equal("token-2", handler.Requests[3].Headers.Authorization?.Parameter);
	}

	[Fact]
	public async Task SearchSeriesAsync_ShouldReturnNormalizedResults_WhenTvdbReturnsSearchPayload()
	{
		const string searchJson = """
			{
				"status": "success",
				"data": [
					{
						"tvdb_id": "121361",
						"name": "Game of Thrones",
						"overview": "Seven noble families fight for control of the mythical land of Westeros.",
						"year": "2011",
						"status": "Ended",
						"network": "HBO",
						"image_url": "https://artworks.thetvdb.com/banners/posters/121361-4.jpg",
						"first_air_time": "2011-04-17"
					}
				]
			}
			""";

		var (client, _) = CreateClient((HttpStatusCode.OK, LoginJson), (HttpStatusCode.OK, searchJson));

		var results = await client.SearchSeriesAsync("game of thrones");

		var result = Assert.Single(results);
		Assert.Equal(121361, result.TvdbId);
		Assert.Equal("Game of Thrones", result.Title);
		Assert.Equal(SeriesStatus.Ended, result.Status);
		Assert.Equal("HBO", result.Network);
		Assert.Equal(new DateOnly(2011, 4, 17), result.FirstAired);
		Assert.Equal(2011, result.Year);
		Assert.Equal("https://artworks.thetvdb.com/banners/posters/121361-4.jpg", result.ImageUrl);
		Assert.Empty(result.Episodes);
	}

	private static (TvdbClient Client, StubHttpMessageHandler Handler) CreateClient(
		params (HttpStatusCode Status, string Json)[] responses)
	{
		var handler = new StubHttpMessageHandler(responses);
		var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://api4.thetvdb.com/v4/")
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Tvdb:ApiKey"] = "test-key" })
			.Build();

		var client = new TvdbClient(httpClient, new MemoryCache(new MemoryCacheOptions()), configuration);

		return (client, handler);
	}
}
