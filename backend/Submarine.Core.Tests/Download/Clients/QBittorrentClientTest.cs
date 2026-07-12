using System.Net;
using System.Security.Cryptography;
using System.Text;
using Submarine.Core.Download;
using Submarine.Core.Download.Clients;
using Submarine.Core.Indexer;
using Submarine.Core.Provider;
using Xunit;

namespace Submarine.Core.Tests.Download.Clients;

public class QBittorrentClientTest
{
	private const string TorrentsJson = """
		[
			{ "hash": "abc123", "name": "Movie.1", "size": 1000, "amount_left": 400, "eta": 120, "state": "downloading", "save_path": "/downloads/Movie.1", "category": "movies" },
			{ "hash": "def456", "name": "Movie.2", "size": 2000, "amount_left": 0, "eta": 8640000, "state": "uploading", "save_path": "/downloads/Movie.2", "category": "movies" },
			{ "hash": "ghi789", "name": "Movie.3", "size": 1500, "amount_left": 1500, "eta": 8640000, "state": "pausedDL", "save_path": "/downloads/Movie.3", "category": "movies" },
			{ "hash": "jkl012", "name": "Movie.4", "size": 500, "amount_left": 500, "eta": -1, "state": "error", "save_path": "/downloads/Movie.4", "category": "movies" }
		]
		""";

	private readonly ITestOutputHelper _output;

	public QBittorrentClientTest(ITestOutputHelper output)
		=> _output = output;

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnMagnetHash_WhenReleaseIsMagnetLink()
	{
		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return LoginSuccessResponse();
			if (path.EndsWith("torrents/add")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Ok.") };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Movie 1",
			Guid = "guid-1",
			DownloadUrl = "magnet:?xt=urn:btih:ABCDEF1234567890ABCDEF1234567890ABCDEF12&dn=Movie",
			Protocol = Protocol.BITTORRENT
		};

		var id = await client.AddDownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal("abcdef1234567890abcdef1234567890abcdef12", id);
		var addCall = handler.Calls.Single(call => call.Request.RequestUri!.AbsolutePath.EndsWith("torrents/add"));
		Assert.Contains("urls=magnet", addCall.Body);
		Assert.Contains("category=movies", addCall.Body);
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldIncludeSeedLimits_WhenSeedCriteriaProvided()
	{
		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return LoginSuccessResponse();
			if (path.EndsWith("torrents/add")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Ok.") };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Movie 1",
			Guid = "guid-1",
			DownloadUrl = "magnet:?xt=urn:btih:ABCDEF1234567890ABCDEF1234567890ABCDEF12&dn=Movie",
			Protocol = Protocol.BITTORRENT
		};

		await client.AddDownloadAsync(release, new SeedCriteria(1.5, 10080, null), TestContext.Current.CancellationToken);

		var addCall = handler.Calls.Single(call => call.Request.RequestUri!.AbsolutePath.EndsWith("torrents/add"));
		Assert.Contains("ratioLimit=1.5", addCall.Body);
		Assert.Contains("seedingTimeLimit=10080", addCall.Body);
	}

	[Fact]
	public async Task AddDownloadAsync_ShouldReturnComputedInfoHash_WhenReleaseIsNotAMagnetLink()
	{
		const string info = "d6:lengthi12e4:name4:teste";
		var torrent = Encoding.ASCII.GetBytes("d8:announce3:url4:info" + info + "e");
		var expected = Convert.ToHexString(SHA1.HashData(Encoding.ASCII.GetBytes(info)));

		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return LoginSuccessResponse();
			if (path.EndsWith("1.torrent")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(torrent) };
			if (path.EndsWith("torrents/add")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Ok.") };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);
		var release = new ReleaseInfo
		{
			Title = "Movie 1",
			Guid = "guid-1",
			DownloadUrl = "https://indexer.example/download/1.torrent",
			Protocol = Protocol.BITTORRENT
		};

		var id = await client.AddDownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(expected, id);
		Assert.Contains(handler.Calls, call => call.Request.RequestUri!.AbsolutePath.EndsWith("torrents/add"));
	}

	[Fact]
	public async Task GetItemsAsync_ShouldMapStatusesAndSizes_WhenQbittorrentReturnsTorrents()
	{
		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return LoginSuccessResponse();
			if (path.EndsWith("torrents/info")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TorrentsJson) };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		Assert.Equal(4, items.Count);

		var downloading = items.Single(item => item.DownloadId == "abc123");
		Assert.Equal(DownloadItemStatus.DOWNLOADING, downloading.Status);
		Assert.Equal(1000, downloading.TotalSize);
		Assert.Equal(400, downloading.RemainingSize);
		Assert.Equal(TimeSpan.FromSeconds(120), downloading.RemainingTime);

		Assert.Equal(DownloadItemStatus.COMPLETED, items.Single(item => item.DownloadId == "def456").Status);
		Assert.Equal(DownloadItemStatus.PAUSED, items.Single(item => item.DownloadId == "ghi789").Status);
		Assert.Equal(DownloadItemStatus.FAILED, items.Single(item => item.DownloadId == "jkl012").Status);
	}

	[Fact]
	public async Task GetItemsAsync_ShouldReLoginOnce_WhenSessionIsForbidden()
	{
		var loginCalls = 0;
		var infoCalls = 0;

		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;

			if (path.EndsWith("auth/login"))
			{
				loginCalls++;
				return LoginSuccessResponse();
			}

			if (path.EndsWith("torrents/info"))
			{
				infoCalls++;
				return infoCalls == 1
					? new HttpResponseMessage(HttpStatusCode.Forbidden)
					: new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TorrentsJson) };
			}

			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		var items = await client.GetItemsAsync(TestContext.Current.CancellationToken);

		Assert.Equal(2, loginCalls);
		Assert.Equal(2, infoCalls);
		Assert.Equal(4, items.Count);
	}

	[Fact]
	public async Task RemoveItemAsync_ShouldSendHashesAndDeleteFiles_WhenCalled()
	{
		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return LoginSuccessResponse();
			if (path.EndsWith("torrents/delete")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Ok.") };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		await client.RemoveItemAsync("abc123", true, TestContext.Current.CancellationToken);

		var deleteCall = handler.Calls.Single(call => call.Request.RequestUri!.AbsolutePath.EndsWith("torrents/delete"));
		Assert.Contains("hashes=abc123", deleteCall.Body);
		Assert.Contains("deleteFiles=true", deleteCall.Body);
	}

	[Fact]
	public async Task TestAsync_ShouldThrowDownloadClientException_WhenLoginFails()
	{
		var handler = new StubHandler((request, _) =>
		{
			var path = request.RequestUri!.AbsolutePath;
			if (path.EndsWith("auth/login")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Fails.") };
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});
		var client = CreateClient(handler);

		await Assert.ThrowsAsync<DownloadClientException>(() => client.TestAsync(TestContext.Current.CancellationToken));
	}

	private static HttpResponseMessage LoginSuccessResponse()
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Ok.") };
		response.Headers.TryAddWithoutValidation("Set-Cookie", "SID=session-id-1; path=/");
		return response;
	}

	private QBittorrentClient CreateClient(HttpMessageHandler handler)
	{
		var settings = new QBittorrentSettings
		{
			Host = "localhost",
			Port = 8080,
			Username = "admin",
			Password = "adminadmin",
			Category = "movies"
		};

		return new QBittorrentClient(settings, new HttpClient(handler), new XunitLogger<QBittorrentClient>(_output));
	}

	private class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

		public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

		public StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
			=> _responder = responder;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
			Calls.Add((request, body));
			return _responder(request, body);
		}
	}
}
