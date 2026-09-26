using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class NzbVortexClientTests
{
	private static NzbVortexSettings Settings()
		=> new() { Host = "vortex.local", Port = 4321, ApiKey = "key123", Category = "tv" };

	private static RemoteRelease NzbRelease()
		=> new()
		{
			Title = "Show.S01E01",
			DownloadUrl = "http://indexer/nzb/1",
			Size = 5,
			Protocol = Protocol.USENET,
			Category = RemoteReleaseCategory.SERIES,
			NzbFile = "nzb-data"u8.ToArray()
		};

	private static StubHttpHandler AuthenticatingHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
		=> new((request, body) =>
		{
			if (request.RequestUri!.AbsolutePath.EndsWith("/auth/nonce"))
				return StubHttpHandler.Json("""{"authNonce":"nonce123"}""");

			if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login"))
				return StubHttpHandler.Json("""{"result":"ok","loginResult":"ok","sessionId":"sess123"}""");

			return respond(request, body);
		});

	[Fact]
	public void Type_ShouldBeNzbVortex()
	{
		var client = new NzbVortexClient(Settings(), 1, "vortex", new HttpClient());

		client.Type.ShouldBe(DownloadClientType.NZBVORTEX);
		client.Protocol.ShouldBe(Protocol.USENET);
	}

	[Fact]
	public async Task AddAsync_ShouldAuthenticateAndUploadNzb_ReturningAddUuid()
	{
		var handler = AuthenticatingHandler((request, _) =>
		{
			request.RequestUri!.AbsolutePath.ShouldContain("nzb/add");
			request.RequestUri!.Query.ShouldContain("sessionid=sess123");
			request.RequestUri!.Query.ShouldContain("groupname=tv");

			return StubHttpHandler.Json("""{"result":"ok","add_uuid":"uuid-42"}""");
		});
		var client = new NzbVortexClient(Settings(), 1, "vortex", new HttpClient(handler));

		var id = await client.AddAsync(NzbRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("uuid-42");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapQueueStates()
	{
		var handler = AuthenticatingHandler((request, _) =>
		{
			request.RequestUri!.AbsolutePath.ShouldContain("/nzb");
			return StubHttpHandler.Json("""
				{
					"result": "ok",
					"items": [
						{ "id": 1, "addUUID": "u1", "uiTitle": "Show.S01E01", "destinationPath": "/data/tv/Show.S01E01", "isPaused": false, "state": 0, "totalDownloadSize": 1000, "downloadedSize": 200, "groupName": "tv" },
						{ "id": 2, "addUUID": "u2", "uiTitle": "Show.S01E02", "destinationPath": "/data/tv/Show.S01E02", "isPaused": false, "state": 20, "totalDownloadSize": 2000, "downloadedSize": 2000, "groupName": "tv" },
						{ "id": 3, "addUUID": "u3", "uiTitle": "Show.S01E03", "destinationPath": "/data/tv/Show.S01E03", "isPaused": true, "state": 1, "totalDownloadSize": 3000, "downloadedSize": 500, "groupName": "tv" },
						{ "id": 4, "addUUID": "u4", "uiTitle": "Bad", "destinationPath": "/data/tv/Bad", "isPaused": false, "state": 24, "totalDownloadSize": 4000, "downloadedSize": 0, "groupName": "tv" }
					]
				}
				""");
		});
		var client = new NzbVortexClient(Settings(), 3, "vortex", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(4);
		items[0].Status.ShouldBe(DownloadItemStatus.QUEUED);
		items[0].DownloadId.ShouldBe("u1");
		items[0].DownloadClientId.ShouldBe(3);
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].Status.ShouldBe(DownloadItemStatus.FAILED);
	}

	[Fact]
	public async Task RemoveAsync_ShouldCancelWithDeleteData_ByAddUuidLookup()
	{
		string? removedPath = null;
		var handler = AuthenticatingHandler((request, _) =>
		{
			if (request.RequestUri!.AbsolutePath.Contains("/cancelDelete"))
			{
				removedPath = request.RequestUri!.AbsolutePath;
				return StubHttpHandler.Json("""{"result":"ok"}""");
			}

			return StubHttpHandler.Json("""
				{ "result": "ok", "items": [ { "id": 7, "addUUID": "u7", "uiTitle": "x", "destinationPath": "/x", "isPaused": false, "state": 0, "totalDownloadSize": 1, "downloadedSize": 0, "groupName": "tv" } ] }
				""");
		});
		var client = new NzbVortexClient(Settings(), 1, "vortex", new HttpClient(handler));

		await client.RemoveAsync("u7", true, TestContext.Current.CancellationToken);

		removedPath.ShouldBe("/api/nzb/7/cancelDelete");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenAuthenticationFails()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.AbsolutePath.EndsWith("/app/appversion"))
				return StubHttpHandler.Json("""{"result":"ok","version":"1.0"}""");

			if (request.RequestUri!.AbsolutePath.EndsWith("/auth/nonce"))
				return StubHttpHandler.Json("""{"authNonce":"nonce123"}""");

			if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login"))
				return StubHttpHandler.Json("""{"result":"ok","loginResult":"failed"}""");

			return StubHttpHandler.Json("""{"result":"not_logged_in"}""");
		});
		var client = new NzbVortexClient(Settings(), 1, "vortex", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("authentication failed");
	}
}
