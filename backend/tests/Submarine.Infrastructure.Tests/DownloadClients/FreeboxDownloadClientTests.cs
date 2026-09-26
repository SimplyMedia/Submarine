using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class FreeboxDownloadClientTests
{
	private static FreeboxDownloadSettings Settings()
		=> new()
		{
			Host = "fb.local",
			Port = 80,
			UseSsl = false,
			ApiUrl = "/api/v1/",
			AppId = "submarine",
			AppToken = "token123"
		};

	private static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));

	private static StubHttpHandler AuthenticatingHandler(Func<HttpRequestMessage, string?, HttpResponseMessage?> respond)
		=> new((request, body) =>
		{
			var path = request.RequestUri!.AbsolutePath;

			if (path == "/api/v1/login" && request.Method == HttpMethod.Get)
				return StubHttpHandler.Json("""{"success":true,"result":{"logged_in":false,"challenge":"chal123"}}""");

			if (path == "/api/v1/login/session")
				return StubHttpHandler.Json("""{"success":true,"result":{"session_token":"sess123"}}""");

			return respond(request, body) ?? throw new InvalidOperationException($"Unexpected request to {path}");
		});

	[Fact]
	public void Type_ShouldBeFreeboxDownload()
	{
		var client = new FreeboxDownloadClient(Settings(), 1, "fb", new HttpClient());

		client.Type.ShouldBe(DownloadClientType.FREEBOX_DOWNLOAD);
		client.Protocol.ShouldBe(Protocol.BITTORRENT);
	}

	[Fact]
	public async Task AddAsync_ShouldAuthenticateAndAddMagnet_ReturningTaskId()
	{
		var handler = AuthenticatingHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;

			if (path == "/api/v1/downloads/config/")
				return StubHttpHandler.Json($"{{\"success\":true,\"result\":{{\"download_dir\":\"{Encode("/data/downloads")}\"}}}}");

			if (path == "/api/v1/downloads/add")
				return StubHttpHandler.Json("""{"success":true,"result":{"id":"42"}}""");

			return null;
		});
		var client = new FreeboxDownloadClient(Settings(), 1, "fb", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("42");
		handler.Requests[0].HasHeader("X-Fbx-App-Auth", "sess123").ShouldBeFalse();
		handler.Requests.Any(r => r.HasHeader("X-Fbx-App-Auth", "sess123")).ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldSetStoppedStatus_WhenAddPaused()
	{
		string? putBody = null;
		var handler = AuthenticatingHandler((request, body) =>
		{
			var path = request.RequestUri!.AbsolutePath;

			if (path == "/api/v1/downloads/config/")
				return StubHttpHandler.Json($"{{\"success\":true,\"result\":{{\"download_dir\":\"{Encode("/data/downloads")}\"}}}}");

			if (path == "/api/v1/downloads/add")
				return StubHttpHandler.Json("""{"success":true,"result":{"id":"42"}}""");

			if (path == "/api/v1/downloads/42")
			{
				putBody = body;
				return StubHttpHandler.Json("""{"success":true,"result":{}}""");
			}

			return null;
		});
		var client = new FreeboxDownloadClient(Settings() with { AddPaused = true }, 1, "fb", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		putBody.ShouldNotBeNull();
		putBody.ShouldContain("\"status\":\"stopped\"");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapBitTorrentTasksOnly()
	{
		var handler = AuthenticatingHandler((request, _) =>
		{
			if (request.RequestUri!.AbsolutePath != "/api/v1/downloads/")
				return null;

			return StubHttpHandler.Json($$"""
				{
					"success": true,
					"result": [
						{ "id": "1", "name": "Show.S01E01", "type": "bt", "download_dir": "{{Encode("/data/downloads")}}", "size": 1000, "rx_pct": 4000, "eta": 30, "status": "downloading", "stop_ratio": 0, "error": "none" },
						{ "id": "2", "name": "Show.S01E02", "type": "bt", "download_dir": "{{Encode("/data/downloads")}}", "size": 2000, "rx_pct": 10000, "eta": 0, "status": "done", "stop_ratio": 200, "error": "none" },
						{ "id": "3", "name": "nzb-task", "type": "nzb", "download_dir": "{{Encode("/data/downloads")}}", "size": 500, "rx_pct": 0, "eta": 0, "status": "queued", "stop_ratio": 0, "error": "none" }
					]
				}
				""");
		});
		var client = new FreeboxDownloadClient(Settings(), 9, "fb", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(2);
		items[0].DownloadId.ShouldBe("1");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingTime.ShouldBe(TimeSpan.FromSeconds(30));
		items[0].DownloadClientId.ShouldBe(9);
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[1].SeedRatio.ShouldBe(2.0);
	}

	[Fact]
	public async Task RemoveAsync_ShouldEraseData_WhenDeleteData()
	{
		string? deletedPath = null;
		var handler = AuthenticatingHandler((request, _) =>
		{
			if (request.Method != HttpMethod.Delete)
				return null;

			deletedPath = request.RequestUri!.AbsolutePath;
			return StubHttpHandler.Json("""{"success":true,"result":{}}""");
		});
		var client = new FreeboxDownloadClient(Settings(), 1, "fb", new HttpClient(handler));

		await client.RemoveAsync("42", true, TestContext.Current.CancellationToken);

		deletedPath.ShouldBe("/api/v1/downloads/42/erase");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenAuthenticationRejected()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;

			if (path == "/api/v1/login" && request.Method == HttpMethod.Get)
				return StubHttpHandler.Json("""{"success":true,"result":{"logged_in":false,"challenge":"chal123"}}""");

			if (path == "/api/v1/login/session")
				return StubHttpHandler.Json("""{"success":false,"msg":"Invalid token","error_code":"invalid_token"}""");

			throw new InvalidOperationException($"Unexpected request to {path}");
		});
		var client = new FreeboxDownloadClient(Settings(), 1, "fb", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("authentication failed");
	}
}
