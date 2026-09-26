using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class RQbitClientTests
{
	private static RQbitSettings Settings()
		=> new() { Host = "rq.local", Port = 3030 };

	[Fact]
	public void Type_ShouldBeRQbit()
	{
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient());

		client.Type.ShouldBe(DownloadClientType.RQBIT);
		client.Protocol.ShouldBe(Protocol.BITTORRENT);
	}

	[Fact]
	public async Task AddAsync_ShouldPostMagnetAndReturnInfoHash()
	{
		string? capturedBody = null;
		var handler = new StubHttpHandler((request, body) =>
		{
			capturedBody = body;
			request.RequestUri!.AbsolutePath.ShouldBe("/torrents");
			request.RequestUri!.Query.ShouldBe("?overwrite=true");

			return StubHttpHandler.Json("""{"id":1,"details":{"info_hash":"abcdef0123456789abcdef0123456789abcdef01"},"output_folder":"/data"}""");
		});
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("ABCDEF0123456789ABCDEF0123456789ABCDEF01");
		capturedBody.ShouldStartWith("magnet:");
	}

	[Fact]
	public async Task AddAsync_ShouldUploadTorrentBytes_WhenTorrentRelease()
	{
		var handler = new StubHttpHandler((_, _) =>
			StubHttpHandler.Json("""{"id":1,"details":{"info_hash":null},"output_folder":"/data"}"""));
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrentsWithStats()
	{
		var handler = new StubHttpHandler((request, _) =>
		{
			request.RequestUri!.AbsolutePath.ShouldBe("/torrents");
			request.RequestUri!.Query.ShouldBe("?with_stats=true");

			return StubHttpHandler.Json("""
				{
					"torrents": [
						{
						"id": 1, "info_hash": "aaa", "name": "Show.S01E01", "output_folder": "/data/downloads/",
							"stats": {
								"state": "Live", "total_bytes": 1000, "progress_bytes": 400, "uploaded_bytes": 100,
								"finished": false, "error": null,
								"live": { "download_speed": { "mbps": 1.0 } }
							}
						},
						{
							"id": 2, "info_hash": "bbb", "name": "Show.S01E02", "output_folder": "/data/downloads/Show.S01E02",
							"stats": {
								"state": "Paused", "total_bytes": 2000, "progress_bytes": 2000, "uploaded_bytes": 400,
								"finished": true, "error": null
							}
						},
						{
							"id": 3, "info_hash": "ccc", "name": "Bad", "output_folder": "/data/downloads",
							"stats": { "state": "Error", "total_bytes": 0, "progress_bytes": 0, "uploaded_bytes": 0, "finished": false, "error": "disk full" }
						}
					]
				}
				""");
		});
		var client = new RQbitClient(Settings(), 4, "rq", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(3);
		items[0].DownloadId.ShouldBe("AAA");
		items[0].OutputPath.ShouldBe("/data/downloads/Show.S01E01");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].DownloadClientId.ShouldBe(4);
		items[1].OutputPath.ShouldBe("/data/downloads/Show.S01E02");
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[2].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[2].Message.ShouldBe("disk full");
	}

	[Fact]
	public async Task RemoveAsync_ShouldDelete_WhenDeleteData()
	{
		string? path = null;
		var handler = new StubHttpHandler((request, _) =>
		{
			path = request.RequestUri!.AbsolutePath;
			return StubHttpHandler.Text("");
		});
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		await client.RemoveAsync("AAA", true, TestContext.Current.CancellationToken);

		path.ShouldBe("/torrents/AAA/delete");
	}

	[Fact]
	public async Task RemoveAsync_ShouldForget_WhenNotDeleteData()
	{
		string? path = null;
		var handler = new StubHttpHandler((request, _) =>
		{
			path = request.RequestUri!.AbsolutePath;
			return StubHttpHandler.Text("");
		});
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		await client.RemoveAsync("AAA", false, TestContext.Current.CancellationToken);

		path.ShouldBe("/torrents/AAA/forget");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenVersionBelowMinimum()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json("""{"version":"7.9.0","server":"rqbit"}"""));
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("8.0.0");
	}

	[Fact]
	public async Task TestAsync_ShouldSucceed_WhenVersionMeetsMinimum()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json("""{"version":"8.0.1","server":"rqbit"}"""));
		var client = new RQbitClient(Settings(), 1, "rq", new HttpClient(handler));

		await client.TestAsync(TestContext.Current.CancellationToken);
	}
}
