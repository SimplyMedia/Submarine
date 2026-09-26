using System.Net;
using System.Text;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Submarine.Core.Entities;
using Submarine.Core.Enums;
using Submarine.Infrastructure.ImportLists;
using Submarine.Infrastructure.Metadata;
using Submarine.Infrastructure.Mappings;

namespace Submarine.Infrastructure.Tests.ImportLists;

/// <summary>
///     Parses each import list source with stub HTTP responses.
/// </summary>
public sealed class ImportListParserTests
{
	[Fact]
	public async Task TraktImportList_ShouldParseWrappedMovieAndShowNodes()
	{
		var json = """
			[
				{"listed_at": "2026-01-01T00:00:00.000Z", "type": "movie", "movie": {"title": "Dune", "year": 2021, "ids": {"trakt": 1, "tmdb": 43863, "imdb": "tt15239678"}}},
				{"listed_at": "2026-01-02T00:00:00.000Z", "type": "show", "show": {"title": "Severance", "year": 2022, "ids": {"trakt": 2, "tmdb": 95396}}}
			]
			""";
		var list = new ImportList
		{
			Name = "trakt watchlist",
			Type = ImportListType.TRAKT_USER,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = """{"user": "me", "listType": "watchlist", "clientId": "abc"}"""
		};

		var items = await new TraktImportList(Factory(HttpStatusCode.OK, json), Config("Trakt:ClientId", "fallback"))
			.FetchAsync(list, TestContext.Current.CancellationToken);

		items.Count.ShouldBe(2);
		items[0].Title.ShouldBe("Dune");
		items[0].TmdbId.ShouldBe(43863);
		items[0].ImdbId.ShouldBe("tt15239678");
		items[1].Title.ShouldBe("Severance");
		items[1].Year.ShouldBe(2022);
	}

	[Fact]
	public async Task TraktImportList_ShouldUseClientIdFromConfig_WhenSettingMissing()
	{
		var list = new ImportList
		{
			Name = "trending",
			Type = ImportListType.TRAKT_POPULAR,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = """{"category": "popular"}"""
		};

		var handler = new RecordingHandler("""[{"title": "Heat", "year": 1995, "ids": {"tmdb": 949}}]""");
		var items = await new TraktImportList(Factory(handler), Config("Trakt:ClientId", "cfg-key")).FetchAsync(list, TestContext.Current.CancellationToken);

		items.Single().Title.ShouldBe("Heat");
		handler.LastRequest!.Headers.GetValues("trakt-api-key").ShouldBe(["cfg-key"]);
		handler.LastRequest.RequestUri!.PathAndQuery.ShouldBe("/movies/popular");
	}

	[Fact]
	public async Task TraktImportList_ShouldFailWithoutClientId()
	{
		var list = new ImportList
		{
			Name = "trending",
			Type = ImportListType.TRAKT_POPULAR,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = "{}"
		};

		await Should.ThrowAsync<InvalidOperationException>(
			async () => await new TraktImportList(Factory(HttpStatusCode.OK, "[]"), Config("Trakt:ClientId", null)).FetchAsync(list));
	}

	[Fact]
	public async Task InstanceImportList_ShouldParseSonarrSeries()
	{
		var json = """
			[
				{"title": "Brooklyn Nine-Nine", "tvdbId": 268582, "year": 2013, "imdbId": "tt2467372"},
				{"title": "Empty", "tvdbId": 0}
			]
			""";
		var list = new ImportList
		{
			Name = "sonarr",
			Type = ImportListType.SONARR,
			MediaKind = MediaKind.SERIES,
			SettingsJson = """{"baseUrl": "http://sonarr:8989", "apiKey": "key"}"""
		};

		var handler = new RecordingHandler(json);
		var items = await new InstanceImportList(Factory(handler)).FetchAsync(list, TestContext.Current.CancellationToken);

		items.Count.ShouldBe(2);
		items[0].TvdbId.ShouldBe(268582);
		items[0].ImdbId.ShouldBe("tt2467372");
		items[1].TvdbId.ShouldBe(0);
		handler.LastRequest!.RequestUri!.ToString().ShouldBe("http://sonarr:8989/api/v3/series");
		handler.LastRequest.Headers.GetValues("X-Api-Key").ShouldBe(["key"]);
	}

	[Fact]
	public async Task InstanceImportList_ShouldParseRadarrMovies()
	{
		var json = """
			[{"title": "Arrival", "tmdbId": 329865, "year": 2016}]
			""";
		var list = new ImportList
		{
			Name = "radarr",
			Type = ImportListType.RADARR,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = """{"baseUrl": "http://radarr:7878/", "apiKey": "key"}"""
		};

		var handler = new RecordingHandler(json);
		var items = await new InstanceImportList(Factory(handler)).FetchAsync(list, TestContext.Current.CancellationToken);

		items.Single().TmdbId.ShouldBe(329865);
		handler.LastRequest!.RequestUri!.ToString().ShouldBe("http://radarr:7878/api/v3/movie");
	}

	[Fact]
	public async Task JsonFeedImportList_ShouldParseStevenLuAndResolveImdbIds()
	{
		var metadata = Substitute.For<IMetadataClient>();
		metadata.GetMovieByImdbAsync("tt0114814", Arg.Any<CancellationToken>())
			.Returns(new Contracts.Metadata.MovieResource(
				10464, "tt0114814", "The Usual Suspects", "The Usual Suspects", null, null,
				null, null, null, Contracts.Metadata.MovieStatus.RELEASED, 1995, 106,
				[], null, null, null, null, null, null, null, []));

		var list = new ImportList
		{
			Name = "stevenlu",
			Type = ImportListType.STEVEN_LU,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = "{}"
		};

		var handler = new RecordingHandler("""[{"imdb_id": "tt0114814", "title": "The Usual Suspects"}]""");
		var items = await new JsonFeedImportList(Factory(handler), metadata).FetchAsync(list, TestContext.Current.CancellationToken);

		items.Single().TmdbId.ShouldBe(10464);
		handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://stevenlu.com/movies.json");
	}

	[Fact]
	public async Task JsonFeedImportList_ShouldParseCustomIds()
	{
		var list = new ImportList
		{
			Name = "custom",
			Type = ImportListType.CUSTOM,
			MediaKind = MediaKind.SERIES,
			SettingsJson = """{"url": "https://example.com/list.json"}"""
		};

		var handler = new RecordingHandler("""
			[
				{"tvdbId": 121361, "title": "Game of Thrones"},
				{"tmdb_id": 1396, "title": "Breaking Bad", "year": 2008},
				{"title": "No id"}
			]
			""");
		var items = await new JsonFeedImportList(Factory(handler), Substitute.For<IMetadataClient>()).FetchAsync(list, TestContext.Current.CancellationToken);

		items.Count.ShouldBe(3);
		items[0].TvdbId.ShouldBe(121361);
		items[1].TmdbId.ShouldBe(1396);
		items[1].Year.ShouldBe(2008);
		items[2].TvdbId.ShouldBeNull();
		items[2].TmdbId.ShouldBeNull();
	}

	[Fact]
	public async Task PlexImportList_ShouldParseWatchlist()
	{
		var metadata = Substitute.For<IMetadataClient>();
		metadata.SearchMoviesAsync("Dune", 2021, Arg.Any<CancellationToken>())
			.Returns([new Contracts.Metadata.SearchResultResource(null, 43863, null, "Dune", 2021, null, null, "released", "tmdb")]);
		metadata.SearchSeriesAsync("Severance", Arg.Any<Core.Enums.MetadataProvider>(), Arg.Any<CancellationToken>())
			.Returns([new Contracts.Metadata.SearchResultResource(391913, null, null, "Severance", 2022, null, null, "continuing", "tvdb")]);

		var json = """
			{"MediaContainer": {"Metadata": [
				{"guid": "plex://movie/1", "title": "Dune", "year": 2021, "type": "movie"},
				{"guid": "plex://show/2", "title": "Severance", "year": 2022, "type": "show"}
			]}}
			""";
		var list = new ImportList
		{
			Name = "plex",
			Type = ImportListType.PLEX,
			MediaKind = MediaKind.MOVIES,
			SettingsJson = """{"accessToken": "token"}"""
		};

		var handler = new RecordingHandler(json);
		var items = await new PlexImportList(Factory(handler), metadata).FetchAsync(list, TestContext.Current.CancellationToken);

		items[0].TmdbId.ShouldBe(43863);
		items[1].TvdbId.ShouldBe(391913);
		handler.LastRequest!.RequestUri!.Query.ShouldContain("X-Plex-Token=token");
	}

	[Fact]
	public async Task AniListImportList_ShouldParseCurrentSeasonAndResolveTvdbId()
	{
		var mappings = Substitute.For<IMappingsClient>();
		mappings.FindByNameAsync("Frieren: Beyond Journey\u0027s End", Arg.Any<CancellationToken>()).Returns([109863]);
		var metadata = Substitute.For<IMetadataClient>();

		var json = """
			{"data": {"Page": {"media": [
				{"id": 154587, "title": {"romaji": "Frieren", "english": "Frieren: Beyond Journey's End"}},
				{"id": 999, "title": {"romaji": "Unmapped"}}
			]}}}
			""";
		metadata.SearchSeriesAsync("Unmapped", Arg.Any<Core.Enums.MetadataProvider>(), Arg.Any<CancellationToken>())
			.Returns([]);
		var list = new ImportList
		{
			Name = "anilist",
			Type = ImportListType.ANILIST_SEASON,
			MediaKind = MediaKind.SERIES,
			SettingsJson = "{}"
		};

		var handler = new RecordingHandler(json);
		var items = await new AniListImportList(
			Factory(handler),
			mappings,
			metadata,
			new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)))
			.FetchAsync(list, TestContext.Current.CancellationToken);

		items.Count.ShouldBe(2);
		items[0].TvdbId.ShouldBe(109863);
		items[1].TvdbId.ShouldBeNull();
		items[1].TmdbId.ShouldBeNull();
		handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
		handler.LastRequest.RequestUri!.ToString().ShouldBe("https://graphql.anilist.co/");
	}

	private static IConfiguration Config(string key, string? value)
	{
		var settings = new Dictionary<string, string?>();
		if (value is not null)
		{
			settings[key] = value;
		}

		return new ConfigurationBuilder()
			.AddInMemoryCollection(settings)
			.Build();
	}

	private static IHttpClientFactory Factory(HttpStatusCode status, string json)
		=> Factory(new RecordingHandler(json));

	private static IHttpClientFactory Factory(HttpMessageHandler handler)
	{
		var services = new ServiceCollection();
		services.AddHttpClient("SubmarineImportLists")
			.ConfigurePrimaryHttpMessageHandler(() => handler);
		return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
	}

	private sealed class RecordingHandler(string json) : HttpMessageHandler
	{
		public HttpRequestMessage? LastRequest { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});
		}
	}
}
