using System.Net;
using Shouldly;
using Xunit;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class FloodClientTests
{
	private static FloodSettings Settings()
		=> new()
		{
			Host = "flood.local", Port = 3000, Username = "admin", Password = "secret",
			Destination = "/data/tv", Tags = ["tv"]
		};

	private static HttpResponseMessage Authenticate(bool ok = true)
	{
		if (!ok)
			return new HttpResponseMessage(HttpStatusCode.Unauthorized);

		var response = StubHttpHandler.Text("""{"success":true}""");
		response.Headers.Add("Set-Cookie", "flood-auth=token123; Path=/; HttpOnly");

		return response;
	}

	private static HttpResponseMessage Torrents()
		=> StubHttpHandler.Json("""
			{
				"torrents": {
					"AAAA": {"name":"Show.S01E01","sizeBytes":1000,"bytesDone":400,"eta":30,"status":["downloading"],"directory":"/data/tv","tags":["tv"]},
					"BBBB": {"name":"Done","sizeBytes":2000,"bytesDone":2000,"eta":-1,"status":["complete"],"directory":"/data/tv","tags":["tv"]},
					"CCCC": {"name":"Stopped","sizeBytes":3000,"bytesDone":1000,"eta":-1,"status":["stopped"],"directory":"/data/tv","tags":["tv"]},
					"DDDD": {"name":"Broken","sizeBytes":4000,"bytesDone":0,"eta":-1,"status":["error"],"directory":"/data/tv","tags":["tv"]},
					"EEEE": {"name":"Foreign","sizeBytes":5000,"bytesDone":0,"eta":-1,"status":["downloading"],"directory":"/data","tags":["other"]}
				}
			}
			""");

	[Fact]
	public async Task AddAsync_ShouldAuthenticateAndAddMagnetWithPayload()
	{
		string? addBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate"))
				return Authenticate();
			if (request.RequestUri!.PathAndQuery.EndsWith("/torrents/add-urls"))
			{
				addBody = body;
				return StubHttpHandler.Json("""{"id":"x"}""");
			}

			return StubHttpHandler.Text("{}");
		});
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
		handler.Requests[0].Url.ShouldContain("/api/auth/authenticate");
		using var payload = JsonDocument.Parse(addBody!);
		payload.RootElement.GetProperty("urls")[0].GetString()
			.ShouldBe("magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=x");
		payload.RootElement.GetProperty("destination").GetString().ShouldBe("/data/tv");
		payload.RootElement.GetProperty("tags")[0].GetString().ShouldBe("tv");
		payload.RootElement.GetProperty("stopped").GetBoolean().ShouldBeFalse();
		var add = handler.Requests.Single(request => request.Url.Contains("/torrents/add-urls"));
		add.HasHeader("Cookie", "flood-auth=token123").ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldAddTorrentFileAndStopped_WhenConfigured()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate")
				? Authenticate()
				: StubHttpHandler.Json("{}"));
		var client = new FloodClient(Settings() with { AddPaused = true }, 8, "flood", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		var add = handler.Requests.Single(request => request.Url.Contains("/torrents/add-files"));
		add.Body!.ShouldContain("name=files");
		add.Body!.ShouldContain("Some.Show.S01E01.torrent");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrentsStatusesTagsAndIdentity()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate")
				? Authenticate()
				: Torrents());
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		items[0].DownloadId.ShouldBe("AAAA");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingSize.ShouldBe(600);
		items[0].RemainingTime.ShouldBe(TimeSpan.FromSeconds(30));
		items[0].OutputPath.ShouldBe("/data/tv");
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(8);
		items[0].DownloadClientName.ShouldBe("flood");
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[4].IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteWithData()
	{
		string? deleteBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			if (request.RequestUri!.PathAndQuery.Contains("/torrents/delete"))
				deleteBody = body;

			return request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate")
				? Authenticate()
				: StubHttpHandler.Json("{}");
		});
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		await client.RemoveAsync("AAAA", true, TestContext.Current.CancellationToken);

		deleteBody.ShouldNotBeNull();
		deleteBody!.ShouldContain("\"hashes\":[\"AAAA\"]");
		deleteBody!.ShouldContain("\"deleteData\":true");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenAuthenticationRejected()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate")
				? Authenticate(ok: false)
				: StubHttpHandler.Text("{}"));
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("authentication failed");
	}

	[Fact]
	public async Task Requests_ShouldReAuthenticate_WhenUnauthorizedMidFlight()
	{
		var authentications = 0;
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate"))
			{
				authentications++;
				return Authenticate();
			}

			return authentications == 1
				? new HttpResponseMessage(HttpStatusCode.Unauthorized)
				: Torrents();
		});
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		authentications.ShouldBe(2);
	}

	[Fact]
	public async Task AddAsync_ShouldIncludeAdditionalTags_WhenConfigured()
	{
		string? addBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate"))
				return Authenticate();
			if (request.RequestUri!.PathAndQuery.EndsWith("/torrents/add-urls"))
			{
				addBody = body;
				return StubHttpHandler.Json("""{"id":"x"}""");
			}

			return StubHttpHandler.Text("{}");
		});
		var settings = Settings() with
		{
			AdditionalTags =
			[
				FloodAdditionalTag.RELEASE_GROUP, FloodAdditionalTag.QUALITY, FloodAdditionalTag.LANGUAGES,
				FloodAdditionalTag.YEAR, FloodAdditionalTag.INDEXER, FloodAdditionalTag.NETWORK
			]
		};
		var client = new FloodClient(settings, 8, "flood", new HttpClient(handler));
		var release = TestTorrent.MagnetRelease() with
		{
			ReleaseGroup = "GROUP", Quality = "WEB_DL-R1080_P", Languages = ["English", "French"], Year = 2024,
			Indexer = "Stub", Network = "ABC"
		};

		await client.AddAsync(release, null, TestContext.Current.CancellationToken);

		using var payload = JsonDocument.Parse(addBody!);
		var tags = payload.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
		tags.ShouldContain("tv");
		tags.ShouldContain("GROUP");
		tags.ShouldContain("WEB_DL-R1080_P");
		tags.ShouldContain("English");
		tags.ShouldContain("French");
		tags.ShouldContain("2024");
		tags.ShouldContain("Stub");
		tags.ShouldContain("ABC");
	}

	[Fact]
	public async Task AddAsync_ShouldIncludeTitleSlug_WhenConfigured()
	{
		string? addBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate"))
				return Authenticate();
			if (request.RequestUri!.PathAndQuery.EndsWith("/torrents/add-urls"))
			{
				addBody = body;
				return StubHttpHandler.Json("""{"id":"x"}""");
			}

			return StubHttpHandler.Text("{}");
		});
		var settings = Settings() with { AdditionalTags = [FloodAdditionalTag.TITLE_SLUG] };
		var client = new FloodClient(settings, 8, "flood", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		using var payload = JsonDocument.Parse(addBody!);
		var tags = payload.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
		tags.ShouldContain("some-show-s01e01");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldMergePostImportTags_WithExistingTags()
	{
		string? patchBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate"))
				return Authenticate();
			if (request.RequestUri!.PathAndQuery.EndsWith("/torrents") && request.Method == HttpMethod.Get)
				return Torrents();
			if (request.RequestUri!.PathAndQuery.EndsWith("/torrents/tags"))
			{
				patchBody = body;
				return StubHttpHandler.Text("{}");
			}

			return StubHttpHandler.Text("{}");
		});
		var client = new FloodClient(Settings() with { PostImportTags = ["imported"] }, 8, "flood",
			new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		using var payload = JsonDocument.Parse(patchBody!);
		payload.RootElement.GetProperty("hashes")[0].GetString().ShouldBe("AAAA");
		var tags = payload.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
		tags.ShouldContain("tv");
		tags.ShouldContain("imported");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldNotCallApi_WhenNoPostImportTagsConfigured()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/auth/authenticate") ? Authenticate() : StubHttpHandler.Text("{}"));
		var client = new FloodClient(Settings(), 8, "flood", new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(0);
	}
}
