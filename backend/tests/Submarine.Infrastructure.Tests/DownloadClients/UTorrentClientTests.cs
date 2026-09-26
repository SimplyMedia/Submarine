using System.Net;
using Shouldly;
using Xunit;
using Submarine.Core.Download;
using Submarine.Core.Provider;
using Submarine.Infrastructure.DownloadClients;

namespace Submarine.Infrastructure.Tests.DownloadClients;

public class UTorrentClientTests
{
	private static UTorrentSettings Settings()
		=> new() { Host = "ut.local", Port = 8080, Username = "admin", Password = "secret", Category = "tv" };

	private static HttpResponseMessage TokenPage()
		=> StubHttpHandler.Text("<html><div id='token'>TOKEN123</div></html>");

	private const string ListJson = """
		{
			"torrents": [
				["AAAA", 137, "Show.S01E01", 1000, 400, 600, 0, 0, 0, 5000, 30, "tv", 1, 2, 3, 4, 1.0, -1, 400, "", "", "", "", "", "", "", "/data/tv"],
				["BBBB", 137, "Done", 2000, 1000, 2000, 0, 0, 0, 0, -1, "tv", 1, 2, 3, 4, 1.0, -1, 0, "", "", "", "", "", "", "", "/data/tv"],
				["CCCC", 169, "Paused", 3000, 0, 0, 0, 0, 0, 0, -1, "tv", 1, 2, 3, 4, 1.0, -1, 3000, "", "", "", "", "", "", "", "/data/tv"],
				["DDDD", 145, "Error", 4000, 0, 100, 0, 0, 0, 0, -1, "tv", 1, 2, 3, 4, 1.0, -1, 3900, "", "", "", "", "", "", "", "/data/tv"],
				["EEEE", 137, "Foreign", 5000, 0, 5000, 0, 0, 0, 0, -1, "other", 1, 2, 3, 4, 1.0, -1, 0, "", "", "", "", "", "", "", "/data"]
			]
		}
		""";

	private static HttpResponseMessage ListResponse()
		=> StubHttpHandler.Json(ListJson);

	[Fact]
	public async Task AddAsync_ShouldFetchTokenAndAddMagnetUrl_WhenMagnetRelease()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html")
				? TokenPage()
				: ListResponse());
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		id.ShouldBe("0123456789ABCDEF0123456789ABCDEF01234567");
		var add = handler.Requests.Single(request => request.Url.Contains("action=add-url"));
		add.Url.ShouldStartWith("http://ut.local:8080/gui/?token=TOKEN123&action=add-url&s=");
		add.Url.ShouldContain(Uri.EscapeDataString("magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=x"));
		add.HasHeader("Authorization", "Basic YWRtaW46c2VjcmV0").ShouldBeTrue();
	}

	[Fact]
	public async Task AddAsync_ShouldUploadTorrentFile_WhenTorrentRelease()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html")
				? TokenPage()
				: StubHttpHandler.Text("{}"));
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		var id = await client.AddAsync(TestTorrent.Release(), null, TestContext.Current.CancellationToken);

		id.ShouldBe(TestTorrent.InfoHash);
		var add = handler.Requests.Single(request => request.Url.Contains("action=add-file"));
		add.Body!.ShouldContain("name=torrent_file");
		add.Body!.ShouldContain("Some.Show.S01E01.torrent");
	}

	[Fact]
	public async Task AddAsync_ShouldStopAndLabel_WhenConfigured()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html")
				? TokenPage()
				: StubHttpHandler.Text("{}"));
		var client = new UTorrentClient(Settings() with { AddStopped = true }, 5, "ut", new HttpClient(handler));

		await client.AddAsync(TestTorrent.MagnetRelease(), null, TestContext.Current.CancellationToken);

		handler.Requests.Single(request => request.Url.Contains("action=stop"))
			.Url.ShouldContain("hash=0123456789ABCDEF0123456789ABCDEF01234567");
		handler.Requests.Single(request => request.Url.Contains("action=setprops"))
			.Url.ShouldContain("s=label&v=tv");
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapTorrentArraysStatusesAndIdentity()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html")
				? TokenPage()
				: ListResponse());
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		items[0].DownloadId.ShouldBe("AAAA");
		items[0].Status.ShouldBe(DownloadItemStatus.DOWNLOADING);
		items[0].RemainingTime.ShouldBe(TimeSpan.FromSeconds(30));
		items[0].TotalSize.ShouldBe(1000);
		items[0].RemainingSize.ShouldBe(400);
		items[0].OutputPath.ShouldBe("/data/tv");
		items[0].Category.ShouldBe("tv");
		items[0].IsReadOnly.ShouldBeFalse();
		items[0].Protocol.ShouldBe(Protocol.BITTORRENT);
		items[0].DownloadClientId.ShouldBe(5);
		items[0].DownloadClientName.ShouldBe("ut");
		items[1].Status.ShouldBe(DownloadItemStatus.COMPLETED);
		items[1].RemainingSize.ShouldBe(0);
		items[2].Status.ShouldBe(DownloadItemStatus.PAUSED);
		items[3].Status.ShouldBe(DownloadItemStatus.FAILED);
		items[4].IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task Requests_ShouldRefreshToken_WhenBadRequest()
	{
		var tokenRequests = 0;
		var handler = new StubHttpHandler((request, _) =>
		{
			if (request.RequestUri!.PathAndQuery.EndsWith("/token.html"))
			{
				tokenRequests++;
				return TokenPage();
			}

			return tokenRequests == 1
				? new HttpResponseMessage(HttpStatusCode.BadRequest)
				: ListResponse();
		});
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		items.Count.ShouldBe(5);
		tokenRequests.ShouldBe(2);
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveWithData_WhenDeleteData()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html")
				? TokenPage()
				: StubHttpHandler.Text("{}"));
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		await client.RemoveAsync("AAAA", true, TestContext.Current.CancellationToken);

		handler.Requests.Single(request => request.Url.Contains("action=removedata"))
			.Url.ShouldContain("hash=AAAA");
	}

	[Fact]
	public async Task TestAsync_ShouldThrow_WhenTokenMissing()
	{
		var handler = new StubHttpHandler((_, _) => StubHttpHandler.Text("<html><body>no token</body></html>"));
		var client = new UTorrentClient(Settings(), 5, "ut", new HttpClient(handler));

		var exception = await Should.ThrowAsync<DownloadClientException>(() =>
			client.TestAsync(TestContext.Current.CancellationToken));

		exception.Message.ShouldContain("token");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldSetLabel_WhenPostImportCategoryDiffersFromCategory()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html") ? TokenPage() : ListResponse());
		var client = new UTorrentClient(Settings() with { PostImportCategory = "imported" }, 5, "ut",
			new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		var setprops = handler.Requests.Single(request => request.Url.Contains("action=setprops"));
		setprops.Url.ShouldContain("hash=AAAA");
		setprops.Url.ShouldContain("s=label");
		setprops.Url.ShouldContain("v=imported");
	}

	[Fact]
	public async Task MarkImportedAsync_ShouldNotCall_WhenPostImportCategoryMatchesCategory()
	{
		var handler = new StubHttpHandler((request, _) =>
			request.RequestUri!.PathAndQuery.EndsWith("/token.html") ? TokenPage() : ListResponse());
		var client = new UTorrentClient(Settings() with { PostImportCategory = "tv" }, 5, "ut", new HttpClient(handler));

		await client.MarkImportedAsync("AAAA", TestContext.Current.CancellationToken);

		handler.Requests.Count.ShouldBe(0);
	}
}
