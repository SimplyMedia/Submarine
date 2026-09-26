using System.Net;
using Shouldly;
using Xunit;
using System.Text;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class TransmissionClientTests
{
	private static TransmissionSettings Settings()
		=> new() { Host = "tr.local", Port = 9091, Category = "tv" };

	private static HttpResponseMessage Success(JsonObject? arguments = null)
		=> StubHttpHandler.Json(new JsonObject
		{
			["result"] = "success",
			["arguments"] = arguments ?? []
		}.ToJsonString());

	[Fact]
	public async Task AddAsync_ShouldRetryWithSessionId_WhenServerReturns409()
	{
		var sessionRequests = 0;
		var handler = new StubHttpHandler((request, _) =>
		{
			sessionRequests++;
			if (sessionRequests == 1)
			{
				var conflict = new HttpResponseMessage(HttpStatusCode.Conflict);
				conflict.Headers.Add("X-Transmission-Session-Id", "sid-1");
				return conflict;
			}

			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject { ["id"] = 5, ["hashString"] = "abcd1234" }
			});
		});
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("ABCD1234");
		sessionRequests.ShouldBe(2);
		handler.Requests[1].HasHeader("X-Transmission-Session-Id", "sid-1").ShouldBeTrue();
		handler.Requests.All(request => request.Url.EndsWith("/transmission/rpc")).ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldSendMagnetViaFilename_WhenMagnetRelease()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject { ["id"] = 5, ["hashString"] = "abcd" }
			});
		});
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		captured!["method"]!.GetValue<string>().ShouldBe("torrent-add");
		var arguments = captured["arguments"]!;
		arguments["filename"]!.GetValue<string>().ShouldStartWith("magnet:");
		arguments["labels"]!.AsArray().Select(label => label!.GetValue<string>()).ShouldBe(["tv"]);
	}

	[Fact]
	public async Task AddAsync_ShouldSendTorrentAsMetainfo_WhenTorrentRelease()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject
				{
					["id"] = 5, ["hashString"] = TestTorrent.InfoHash.ToLowerInvariant()
				}
			});
		});
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		captured!["method"]!.GetValue<string>().ShouldBe("torrent-add");
		captured["arguments"]!["metainfo"]!.GetValue<string>()
			.ShouldBe(Convert.ToBase64String(TestTorrent.Bytes));
	}

	[Fact]
	public async Task AddAsync_ShouldApplyDirectoryAndPaused_WhenConfigured()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject { ["id"] = 5, ["hashString"] = "abcd" }
			});
		});
		var client = new TransmissionClient(Settings() with { Directory = "/data/tv", AddPaused = true }, 1, "tr",
			new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		captured!["arguments"]!["download-dir"]!.GetValue<string>().ShouldBe("/data/tv");
		captured["arguments"]!["paused"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldApplySeedCriteria_AfterAdd()
	{
		var bodies = new List<string>();
		var handler = new StubHttpHandler((_, body) =>
		{
			bodies.Add(body!);
			return Success(new JsonObject
			{
				["torrent-added"] = new JsonObject { ["id"] = 5, ["hashString"] = "abcd" }
			});
		});
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), new SeedCriteria(2.0, 120, null), TestContext.Current.CancellationToken);

		bodies.Count.ShouldBe(2);
		var torrentSet = JsonNode.Parse(bodies[1])!;
		torrentSet["method"]!.GetValue<string>().ShouldBe("torrent-set");
		torrentSet["arguments"]!["seedRatioLimit"]!.GetValue<double>().ShouldBe(2.0);
		torrentSet["arguments"]!["seedRatioMode"]!.GetValue<int>().ShouldBe(1);
		torrentSet["arguments"]!["seedIdleLimit"]!.GetValue<int>().ShouldBe(120);
		torrentSet["arguments"]!["seedIdleMode"]!.GetValue<int>().ShouldBe(1);
		torrentSet["arguments"]!["ids"]!.AsArray()[0]!.GetValue<int>().ShouldBe(5);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapStatusesLabelsAndIdentity()
	{
		var handler = new StubHttpHandler((_, _) => Success(new JsonObject
		{
			["torrents"] = new JsonArray(
				new JsonObject
				{
					["id"] = 1, ["hashString"] = "aaa", ["name"] = "Show.S01E01", ["totalSize"] = 1000,
					["leftUntilDone"] = 400, ["eta"] = 30, ["status"] = 4, ["downloadDir"] = "/data/tv",
					["errorString"] = "", ["labels"] = new JsonArray("tv")
				},
				new JsonObject
				{
					["id"] = 2, ["hashString"] = "bbb", ["name"] = "Show.S01E02", ["totalSize"] = 2000,
					["leftUntilDone"] = 200, ["eta"] = -1, ["status"] = 0, ["downloadDir"] = "/data/tv",
					["errorString"] = "", ["labels"] = new JsonArray("tv")
				},
				new JsonObject
				{
					["id"] = 3, ["hashString"] = "ccc", ["name"] = "Broken", ["totalSize"] = 3000,
					["leftUntilDone"] = 3000, ["eta"] = -1, ["status"] = 4, ["downloadDir"] = "/data/tv",
					["errorString"] = "tracker failure", ["labels"] = new JsonArray()
				},
				new JsonObject
				{
					["id"] = 4, ["hashString"] = "ddd", ["name"] = "Foreign", ["totalSize"] = 4000,
					["leftUntilDone"] = 0, ["eta"] = -1, ["status"] = 0, ["downloadDir"] = "/data",
					["errorString"] = "", ["labels"] = new JsonArray("other")
				})
		}));
		var client = new TransmissionClient(Settings(), 3, "tr", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(4);
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingTime.ShouldBe(TimeSpan.FromSeconds(30));
		items[1].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[1].Category.ShouldBe("tv");
		items[1].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[1].DownloadClientId.ShouldBe(3);
		items[2].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[2].Message.ShouldBe("tracker failure");
		items[3].IsReadOnly.ShouldBeTrue();

		var fields = JsonNode.Parse(handler.Requests[0].Body!)!["arguments"]!["fields"]!.AsArray();
		fields.Select(field => field!.GetValue<string>()).ShouldContain("labels");
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveWithLocalData_WhenDeleteData()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Success();
		});
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		await client.RemoveAsync("aaa", true, TestContext.Current.CancellationToken);

		captured!["method"]!.GetValue<string>().ShouldBe("torrent-remove");
		captured["arguments"]!["ids"]!.AsArray()[0]!.GetValue<string>().ShouldBe("aaa");
		captured["arguments"]!["delete-local-data"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenResultIsNotSuccess()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json("""{"result":"unrecognized info"}"""));
		var client = new TransmissionClient(Settings(), 1, "tr", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("unrecognized info");
	}

	[Fact]
	public async Task Requests_ShouldSendBasicAuth_WhenCredentialsConfigured()
	{
		var handler = new StubHttpHandler((_, _) => Success());
		var client = new TransmissionClient(Settings() with { Username = "user", Password = "pass" }, 1, "tr",
			new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);

		handler.Requests[0].Headers.Authorization.ShouldNotBeNull();
		handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
		handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe(
			Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")));
	}
}
