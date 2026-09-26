using Shouldly;
using Xunit;
using System.Text.Json;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class Aria2ClientTests
{
	private static Aria2Settings Settings()
		=> new() { Host = "aria.local", Port = 6800, SecretToken = "s3cret", Directory = "/data/tv" };

	private static HttpResponseMessage Rpc(object result)
		=> StubHttpHandler.Json(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "x", result }));

	private static HttpResponseMessage Envelope(string resultJson)
		=> StubHttpHandler.Json($$"""{"jsonrpc":"2.0","id":"x","result":{{resultJson}}}""");

	[Fact]
	public async Task AddAsync_ShouldAddUriWithTokenAndDirectory_WhenMagnetRelease()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return Rpc("gid-1");
		});
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("gid-1");
		using var document = JsonDocument.Parse(requestBody!);
		var root = document.RootElement;
		root.GetProperty("method").GetString().ShouldBe("aria2.addUri");
		var parameters = root.GetProperty("params");
		parameters[0].GetString().ShouldBe("token:s3cret");
		parameters[1][0].GetString().ShouldStartWith("magnet:");
		parameters[2].GetProperty("dir").GetString().ShouldBe("/data/tv");
		handler.Requests[0].Url.ShouldBe("http://aria.local:6800/rpc");
	}

	[Fact]
	public async Task AddAsync_ShouldAddTorrentBase64_WhenTorrentRelease()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return Rpc("gid-2");
		});
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("gid-2");
		using var document = JsonDocument.Parse(requestBody!);
		var parameters = document.RootElement.GetProperty("params");
		document.RootElement.GetProperty("method").GetString().ShouldBe("aria2.addTorrent");
		parameters[1].GetString().ShouldBe(Convert.ToBase64String(TestTorrent.Bytes));
		parameters[2].GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task AddAsync_ShouldIncludeSeedOptions_WhenSeedCriteriaProvided()
	{
		string? requestBody = null;
		var handler = new StubHttpHandler((_, body) =>
		{
			requestBody = body;
			return Rpc("gid-3");
		});
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), new SeedCriteria(2.5, 90, null), TestContext.Current.CancellationToken);

		using var document = JsonDocument.Parse(requestBody!);
		var options = document.RootElement.GetProperty("params")[2];
		options.GetProperty("seed-ratio").GetString().ShouldBe("2.5");
		options.GetProperty("seed-time").GetString().ShouldBe("90");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldCombineActiveWaitingStoppedAndMapStatuses()
	{
		static string Download(string gid, string status, string dir, string path)
			=> JsonSerializer.Serialize(new Dictionary<string, object>
			{
				["gid"] = gid,
				["totalLength"] = "1000",
				["completedLength"] = status == "complete" ? "1000" : "400",
				["status"] = status,
				["dir"] = dir,
				["files"] = new[] { new Dictionary<string, string> { ["path"] = path } }
			});
		var calls = new List<string>();
		var handler = new StubHttpHandler((_, body) =>
		{
			var method = JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString();
			calls.Add(method!);
			return method switch
			{
				"aria2.tellActive" => Envelope($"[{Download("A", "active", "/data/tv", "/data/tv/Show.S01E01.mkv")}]"),
				"aria2.tellWaiting" => Envelope($"[{Download("B", "waiting", "/data/tv", "/data/tv/Queued.mkv")}]"),
				_ => Envelope(
					$"[{Download("C", "paused", "/data/tv", "/data/tv/Paused.mkv")},{Download("D", "complete", "/data/tv", "/data/tv/Done.mkv")},{Download("E", "error", "/data/tv", "/data/tv/Broken.mkv")}]")
			};
		});
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		calls.ShouldBe(["aria2.tellActive", "aria2.tellWaiting", "aria2.tellStopped"], ignoreOrder: false);
		items[0].DownloadId.ShouldBe("A");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].Title.ShouldBe("Show.S01E01.mkv");
		items[0].RemainingSize.ShouldBe(600);
		items[0].OutputPath.ShouldBe("/data/tv");
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(6);
		items[1].Status.ShouldBe(DownloadItemStatus.QUEUED);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[3].RemainingSize.ShouldBe(0);
		items[4].Status.ShouldBe(DownloadItemStatus.FAILED);
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveAndClearResult()
	{
		var methods = new List<string>();
		var handler = new StubHttpHandler((_, body) =>
		{
			methods.Add(JsonDocument.Parse(body!).RootElement.GetProperty("method").GetString()!);
			return Rpc("ok");
		});
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		await client.RemoveAsync("gid-1", true, TestContext.Current.CancellationToken);

		methods.ShouldBe(["aria2.remove", "aria2.removeDownloadResult"]);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowWithRpcMessage_WhenSecretRejected()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(
			"""{"jsonrpc":"2.0","id":"x","error":{"code":1,"message":"Unauthorized"}}"""));
		var client = new Aria2Client(Settings(), 6, "aria", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("Unauthorized");
	}
}
