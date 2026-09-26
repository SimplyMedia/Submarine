using Shouldly;
using Xunit;
using System.Net;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class QBittorrentClientTests
{
	private const string TorrentsJson = """
		[
			{"hash":"AAAA","name":"Show.S01E01","size":1000,"amount_left":400,"eta":60,"state":"downloading","save_path":"/data/tv","category":"tv","max_ratio":-1,"max_seeding_time":-1},
			{"hash":"BBBB","name":"Show.S01E02","size":2000,"amount_left":0,"eta":8640000,"state":"stoppedUP","save_path":"/data/tv","category":"tv","max_ratio":2.5,"max_seeding_time":600},
			{"hash":"CCCC","name":"Other.S01","size":3000,"amount_left":3000,"eta":-1,"state":"error","save_path":"/data/tv","category":"","max_ratio":-1,"max_seeding_time":-1},
			{"hash":"DDDD","name":"Foreign","size":4000,"amount_left":0,"eta":-1,"state":"pausedUP","save_path":"/data","category":"other","max_ratio":-1,"max_seeding_time":-1}
		]
		""";

	private static QBittorrentSettings Settings()
		=> new() { Host = "qb.local", Port = 8080, Category = "tv" };

	[Fact]
	public async Task AddAsync_ShouldLoginAndSendMagnetWithFields_WhenMagnetRelease()
	{
		var settings = Settings() with { InitialState = QBittorrentInitialState.PAUSE, SequentialOrder = true, FirstAndLast = true };
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=abc123; path=/");
				return response;
			}

			return StubHttpHandler.Text("Ok.");
		});
		var client = new QBittorrentClient(settings, 1, "qb", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
		handler.Requests.Count.ShouldBe(2);
		var add = handler.Requests[1];
		add.Url.ShouldContain("/api/v2/torrents/add");
		add.HasHeader("Cookie", "SID=abc123").ShouldBeTrue();
		add.Body!.ShouldContain("urls=magnet%3A%3Fxt%3Durn%3Abtih%3A0123456789ABCDEF0123456789ABCDEF01234567");
		add.Body!.ShouldContain("category=tv");
		add.Body!.ShouldContain("stopped=true");
		add.Body!.ShouldContain("sequentialDownload=true");
		add.Body!.ShouldContain("firstLastPiecePrio=true");
	}

	[Fact]
	public async Task AddAsync_ShouldUploadTorrentFileAndReturnInfoHash_WhenTorrentRelease()
	{
		var settings = Settings() with { Host = "qb.local" };
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Text("Ok.");
		});
		var client = new QBittorrentClient(settings, 1, "qb", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		var add = handler.Requests[1];
		add.Url.ShouldContain("/api/v2/torrents/add");
		add.Body!.ShouldContain("name=torrents");
		add.Body!.ShouldContain("Some.Show.S01E01.torrent");
		add.Body!.ShouldContain("name=category");
		add.Body!.ShouldContain("tv");
	}

	[Fact]
	public async Task AddAsync_ShouldForceStart_WhenInitialStateForceStart()
	{
		var settings = Settings() with { InitialState = QBittorrentInitialState.FORCE_START };
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Text("Ok.");
		});
		var client = new QBittorrentClient(settings, 1, "qb", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		handler.Requests.ShouldContain(request =>
			request.Url.EndsWith("/api/v2/torrents/setForceStart")
			&& request.Body!.Contains("hashes=0123456789ABCDEF0123456789ABCDEF01234567")
			&& request.Body.Contains("value=true"));
	}

	[Fact]
	public async Task AddAsync_ShouldIncludeSeedLimits_WhenSeedCriteriaProvided()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Text("Ok.");
		});
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), new SeedCriteria(1.5, 90, 1000), TestContext.Current.CancellationToken);

		handler.Requests[1].Body!.ShouldContain("ratioLimit=1.5");
		handler.Requests[1].Body!.ShouldContain("seedingTimeLimit=90");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapStatusesOutputPathAndIdentity_WhenTorrentsReturned()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Json(TorrentsJson);
		});
		var client = new QBittorrentClient(Settings(), 7, "my-qb", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(4);
		handler.Requests.Last().Url.ShouldContain("/api/v2/torrents/info?category=tv");

		var downloading = items[0];
		downloading.DownloadId.ShouldBe("AAAA");
		downloading.Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		downloading.TotalSize.ShouldBe(1000);
		downloading.RemainingSize.ShouldBe(400);
		downloading.RemainingTime.ShouldBe(TimeSpan.FromSeconds(60));
		downloading.OutputPath.ShouldBe("/data/tv");
		downloading.Category.ShouldBe("tv");
		downloading.IsReadOnly.ShouldBeFalse();
		downloading.CanBeRemoved.ShouldBeTrue();
		downloading.CanMoveFiles.ShouldBeTrue();
		downloading.Protocol.ShouldBe(Protocol.BITTORRENT);
		downloading.DownloadClientId.ShouldBe(7);
		downloading.DownloadClientName.ShouldBe("my-qb");

		var seeding = items[1];
		seeding.Status.ShouldBe(DownloadItemStatus.COMPLETED);
		seeding.RemainingTime.ShouldBeNull();
		seeding.SeedRatio.ShouldBe(2.5);
		seeding.SeedTime.ShouldBe(TimeSpan.FromMinutes(10));
		seeding.CanMoveFiles.ShouldBeFalse();

		items[2].Status.ShouldBe(DownloadItemStatus.FAILED);

		var foreign = items[3];
		foreign.IsReadOnly.ShouldBeTrue();
		foreign.CanBeRemoved.ShouldBeFalse();
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReLoginOnce_WhenSessionIsForbidden()
	{
		var logins = 0;
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				logins++;
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=fresh;");
				return response;
			}

			return logins == 1
				? new HttpResponseMessage(HttpStatusCode.Forbidden)
				: StubHttpHandler.Json("[]");
		});
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(0);
		logins.ShouldBe(2);
		handler.Requests.Count(request => request.Url.Contains("/torrents/info")).ShouldBe(2);
	}

	[Fact]
	public async Task RemoveAsync_ShouldSendHashesAndDeleteFiles_WhenDeleteData()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Text(string.Empty);
		});
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		await client.RemoveAsync("AAAA", true, TestContext.Current.CancellationToken);

		var remove = handler.Requests.Single(request => request.Url.Contains("/torrents/delete"));
		remove.Body!.ShouldContain("hashes=AAAA");
		remove.Body!.ShouldContain("deleteFiles=true");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldSetPostImportCategory_WhenConfigured()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Text(string.Empty);
		});
		var client = new QBittorrentClient(Settings() with { PostImportCategory = "imported" }, 1, "qb",
			new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		var request = handler.Requests.Single(request => request.Url.Contains("/torrents/setCategory"));
		request.Body!.ShouldContain("hashes=AAAA");
		request.Body!.ShouldContain("category=imported");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldNotCallApi_WhenPostImportCategoryMissing()
	{
		var handler = new StubHttpHandler((request, _) => StubHttpHandler.Text(string.Empty));
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(0);
	}

	[Fact]
	public async Task AddAsync_ShouldSendContentLayout_WhenConfigured()
	{
		var settings = Settings() with { ContentLayout = QBittorrentContentLayout.SUBFOLDER };
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=abc123; path=/");
				return response;
			}

			return StubHttpHandler.Text("Ok.");
		});
		var client = new QBittorrentClient(settings, 1, "qb", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		var add = handler.Requests.Single(request => request.Url.Contains("/torrents/add"));
		add.Body!.ShouldContain("contentLayout=Subfolder");
	}

	[Fact]
	public async Task Requests_ShouldUseBearerToken_WhenApiKeyConfigured_SkippingCookieLogin()
	{
		var settings = Settings() with { ApiKey = "secret-key" };
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Text(string.Empty));
		var client = new QBittorrentClient(settings, 1, "qb", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(1);
		handler.Requests[0].Url.ShouldNotContain("/auth/login");
		handler.Requests[0].Headers.Authorization.ShouldNotBeNull();
		handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
		handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("secret-key");
	}

	[Fact]
	public async Task TestAsync_ShouldThrowActionableMessage_WhenLoginRejected()
	{
		var handler = new StubHttpHandler((request, _) => StubHttpHandler.Text("Fails."));
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("login failed");
	}

	[Fact]
	public async Task GetStatusAsync_ShouldReportDistinctSavePaths()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/auth/login"))
			{
				var response = StubHttpHandler.Text("Ok.");
				response.Headers.Add("Set-Cookie", "SID=s;");
				return response;
			}

			return StubHttpHandler.Json(TorrentsJson);
		});
		var client = new QBittorrentClient(Settings(), 1, "qb", new HttpClient(handler));

		var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

		status.OutputRootFolders.ShouldBe(["/data/tv", "/data"], ignoreOrder: true);
	}
}
