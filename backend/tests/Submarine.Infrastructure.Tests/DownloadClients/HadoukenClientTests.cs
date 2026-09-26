using Shouldly;
using Xunit;
using System.Text.Json.Nodes;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class HadoukenClientTests
{
	private static HadoukenSettings Settings()
		=> new() { Host = "hk.local", Port = 7070, Username = "admin", Password = "secret", Category = "tv" };

	private static HttpResponseMessage Rpc(object result)
		=> StubHttpHandler.Json(new JsonObject { ["result"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(result)) }.ToJsonString());

	[Fact]
	public async Task AddAsync_ShouldSendMagnetWithLabel_WhenMagnetRelease()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Rpc("ok");
		});
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
		captured!["method"]!.GetValue<string>().ShouldBe("webui.addTorrent");
		var parameters = captured["params"]!.AsArray();
		parameters[0]!.GetValue<string>().ShouldBe("url");
		parameters[1]!.GetValue<string>().ShouldStartWith("magnet:");
		parameters[2]!["label"]!.GetValue<string>().ShouldBe("tv");
		handler.Requests[0].Headers.Authorization.ShouldNotBeNull();
		handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Basic");
		handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe(
			Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:secret")));
	}

	[Fact]
	public async Task AddAsync_ShouldUploadTorrentFileBase64_WhenTorrentRelease()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Rpc("ok");
		});
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		var parameters = captured!["params"]!.AsArray();
		parameters[0]!.GetValue<string>().ShouldBe("file");
		parameters[1]!.GetValue<string>().ShouldBe(Convert.ToBase64String(TestTorrent.Bytes));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapStatusesAndFilterByLabel()
	{
		var handler = new StubHttpHandler((_, _) => Rpc(new
		{
			torrents = new object[]
			{
				new object[] { "aaa", 1, "Show.S01E01", 1000L, 400.0, 600L, 0L, 0, 0, 500L, 0, "tv", 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, 0, "/data/tv" },
				new object[] { "bbb", 9, "Show.S01E02", 2000L, 1000.0, 2000L, 0L, 0, 0, 0L, 0, "tv", 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, 0, "/data/tv" },
				new object[] { "ccc", 4, "Broken", 3000L, 0.0, 0L, 0L, 0, 0, 0L, 0, "tv", 0, 0, 0, 0, 0, 0, 0, 0, 0, "tracker failure", 0, 0, 0, 0, "/data/tv" },
				new object[] { "ddd", 0, "Other", 4000L, 0.0, 0L, 0L, 0, 0, 0L, 0, "movies", 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, 0, "/data/movies" }
			}
		}));
		var client = new HadoukenClient(Settings(), 3, "hk", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(3);
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].DownloadId.ShouldBe("AAA");
		items[0].DownloadClientId.ShouldBe(3);
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.WARNING);
		items[2].Message.ShouldBe("tracker failure");
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveData_WhenDeleteData()
	{
		JsonObject? captured = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			captured = JsonNode.Parse(body!)!.AsObject();
			return Rpc(true);
		});
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		await client.RemoveAsync("AAA", true, TestContext.Current.CancellationToken);

		captured!["method"]!.GetValue<string>().ShouldBe("webui.perform");
		captured["params"]!.AsArray()[0]!.GetValue<string>().ShouldBe("removedata");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenVersionBelowMinimum()
	{
		var handler = new StubHttpHandler((_, _) =>
			Rpc(new { versions = new Dictionary<string, string> { ["hadouken"] = "5.0.0" } }));
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("5.0.0");
	}

	[Fact]
	public async Task TestAsync_ShouldSucceed_WhenVersionMeetsMinimum()
	{
		var handler = new StubHttpHandler((_, _) =>
			Rpc(new { versions = new Dictionary<string, string> { ["hadouken"] = "5.1.2" } }));
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenRpcReturnsError()
	{
		var handler = new StubHttpHandler((_, _) =>
			StubHttpHandler.Json("""{"error":"unauthorized"}"""));
		var client = new HadoukenClient(Settings(), 1, "hk", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("unauthorized");
	}
}
